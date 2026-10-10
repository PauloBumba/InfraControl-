using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Integrations.Unifi;

/// <summary>
/// F20-T001/ADR-028 §2 + Fase 1 (F26-T001): cliente REST de saída.
/// Login com cookie de sessão <c>unifises</c>; o <b>estilo do controlador</b>
/// é detectado uma única vez na primeira autenticação (docs/35 §3):
/// UniFi OS (UDM/Cloud Key) expõe <c>POST /api/auth/login</c> e prefixa a
/// API com <c>/proxy/network</c>; standalone UniFi Network Application
/// rejeita aquele login com 401 e usa <c>POST /api/login</c> com a API na
/// raiz. Re-autenticação automática uma vez quando o Controller responde
/// 401 (sessão expirada). Resiliência (retry/circuit breaker) fica no
/// handler do <c>IHttpClientFactory</c> (named client <c>unifi</c>, política
/// reaproveitada do dispatcher de webhook — ADR-028 §6).
/// </summary>
public sealed class UnifiApiClient : IUnifiApiClient
{
    public const string HttpClientName = "unifi";

    internal const string UniFiOsLoginPath = "/api/auth/login";
    internal const string StandaloneLoginPath = "/api/login";

    private static readonly Regex UnifisesCookie = new(
        @"unifises=([^;]+)", RegexOptions.Compiled);

    private readonly IHttpClientFactory _factory;
    private readonly UnifiIntegrationOptions _options;
    private readonly ILogger<UnifiApiClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _sessionCookie;
    private UniFiApiStyle _apiStyle = UniFiApiStyle.Unknown;

    public UnifiApiClient(
        IHttpClientFactory factory,
        IOptions<UnifiIntegrationOptions> options,
        ILogger<UnifiApiClient> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UnifiDeviceDto>> ListDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => DeviceStatsUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseDevices(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<UnifiClientTrafficDto>> ListClientTrafficAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => ClientStatsUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseClientTraffic(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<UnifiDpiAppDto>> ListDpiStatsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => DpiStatsUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseDpiStats(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            return false;

        try
        {
            var http = _factory.CreateClient(HttpClientName);
            using var response = await GetWithSessionAsync(
                http, () => SelfUri, reauthenticateOn401: true, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception,
                "Health do UniFi Controller falhou ({Provider})", "Unifi");
            return false;
        }
    }

    private async Task<HttpResponseMessage> GetWithSessionAsync(
        HttpClient http,
        Func<string> uri,
        bool reauthenticateOn401,
        CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(http, cancellationToken);

        var response = await SendWithSessionAsync(http, uri(), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            reauthenticateOn401)
        {
            response.Dispose();
            await LoginAsync(http, invalidateSession: true, cancellationToken);
            response = await SendWithSessionAsync(http, uri(), cancellationToken);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendWithSessionAsync(
        HttpClient http, string uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (_sessionCookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", _sessionCookie);

        return await http.SendAsync(request, cancellationToken);
    }

    private async Task EnsureAuthenticatedAsync(
        HttpClient http, CancellationToken cancellationToken)
    {
        if (_sessionCookie is not null)
            return;

        await LoginAsync(http, invalidateSession: false, cancellationToken);
    }

    private async Task LoginAsync(
        HttpClient http, bool invalidateSession, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (invalidateSession)
                _sessionCookie = null;

            if (_sessionCookie is not null)
                return;

            var cookie = _apiStyle == UniFiApiStyle.Unknown
                ? await DetectStyleAndLoginAsync(http, cancellationToken)
                : await LoginKnownStyleAsync(http, cancellationToken);

            _sessionCookie = cookie;
            _logger.LogDebug("Sessão UniFi obtida ({Provider}, estilo {Style})",
                "Unifi", _apiStyle);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Primeira autenticação: tenta o login do UniFi OS; se rejeitado
    /// (401/403 — standalone responde 401 até com credenciais válidas —
    /// ou 404/405 — endpoint inexistente), tenta o login legado standalone.
    /// </summary>
    private async Task<string> DetectStyleAndLoginAsync(
        HttpClient http, CancellationToken cancellationToken)
    {
        using var first = await PostLoginAsync(http, UniFiOsLoginPath, cancellationToken);

        if (first.IsSuccessStatusCode)
            return ResolveCookie(first, UniFiApiStyle.UniFiOs);

        if (!IsAuthRejection(first) && !IsUnsupported(first))
            first.EnsureSuccessStatusCode(); // 5xx e demais → HttpRequestException

        using var second = await PostLoginAsync(http, StandaloneLoginPath, cancellationToken);

        if (second.IsSuccessStatusCode)
            return ResolveCookie(second, UniFiApiStyle.Standalone);

        // 401/403 em qualquer tentativa = credencial recusada (num controlador
        // UniFi OS ruim, /api/login pode nem existir — o 401 do primeiro tento
        // já basta para falar em credenciais).
        if (IsAuthRejection(second) || IsAuthRejection(first))
            throw new UnauthorizedAccessException(
                "Credenciais do UniFi Controller inváladas ou sessão recusada.");

        throw new InvalidOperationException(
            "UniFi Controller não expôs API de login compatível " +
            "(/api/auth/login ou /api/login).");
    }

    private async Task<string> LoginKnownStyleAsync(
        HttpClient http, CancellationToken cancellationToken)
    {
        var path = _apiStyle == UniFiApiStyle.UniFiOs
            ? UniFiOsLoginPath
            : StandaloneLoginPath;

        using var response = await PostLoginAsync(http, path, cancellationToken);

        if (IsAuthRejection(response))
            throw new UnauthorizedAccessException(
                "Credenciais do UniFi Controller inváladas ou sessão recusada.");

        response.EnsureSuccessStatusCode();

        return ResolveCookie(response, _apiStyle);
    }

    private async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient http, string path, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new { username = _options.Username, password = _options.Password });

        return await http.PostAsync(
            $"{BaseUrl}{path}",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);
    }

    private static bool IsAuthRejection(HttpResponseMessage response)
        => response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    private static bool IsUnsupported(HttpResponseMessage response)
        => response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed;

    private string ResolveCookie(HttpResponseMessage response, UniFiApiStyle style)
    {
        _apiStyle = style;
        _logger.LogInformation(
            "UniFi Controller identificado como {Style} ({Provider})", style, "Unifi");

        return ExtractSessionCookie(response)
            ?? throw new InvalidOperationException(
                "UniFi Controller não devolveu o cookie de sessão unifises.");
    }

    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        foreach (var value in values)
        {
            var match = UnifisesCookie.Match(value);
            if (match.Success)
                return match.Value;
        }

        return null;
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException(
                "Integração UniFi não configurada (Integrations:Unifi:BaseUrl/Username/Password).");
    }

    private string SelfUri => $"{BaseUrl}/api/self";

    private string DeviceStatsUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/device";

    /// <summary>Clientes ativos com contadores rx/tx (F18-T007 · Insights).</summary>
    private string ClientStatsUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/sta";

    /// <summary>DPI por aplicação (F18-T007 · Insights).</summary>
    private string DpiStatsUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/app";

    /// <summary>Prefixo da API: UniFi OS usa <c>/proxy/network</c>, standalone usa a raiz.</summary>
    private string ApiPrefix
        => _apiStyle == UniFiApiStyle.UniFiOs ? "/proxy/network" : string.Empty;

    private string BaseUrl => _options.BaseUrl.TrimEnd('/');

    private static IReadOnlyList<UnifiDeviceDto> ParseDevices(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");

        var devices = new List<UnifiDeviceDto>();
        foreach (var item in data.EnumerateArray())
        {
            devices.Add(new UnifiDeviceDto(
                Read(item, "mac"),
                Read(item, "name"),
                Read(item, "model"),
                Read(item, "type"),
                Read(item, "ip")));
        }

        return devices;
    }

    /// <summary>
    /// F18-T007: <c>GET /stat/sta</c> devolve os clientes ativos com
    /// <c>rx_bytes</c>/<c>tx_bytes</c> (acumulados desde o reset do
    /// cliente) e <c>last_seen</c> em epochs seconds (Unix).
    /// </summary>
    private static IReadOnlyList<UnifiClientTrafficDto> ParseClientTraffic(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");

        var clients = new List<UnifiClientTrafficDto>();
        foreach (var item in data.EnumerateArray())
        {
            clients.Add(new UnifiClientTrafficDto(
                Read(item, "mac"),
                Read(item, "hostname"),
                Read(item, "ip"),
                ReadLong(item, "rx_bytes"),
                ReadLong(item, "tx_bytes"),
                ReadUnixSeconds(item, "last_seen")));
        }

        return clients;
    }

    /// <summary>
    /// F18-T007: <c>GET /stat/app</c> devolve uma linha por aplicação
    /// inspecionada — <c>app</c> (nome), <c>cat</c> (categoria; alguns
    /// controladores usam <c>category</c>) e <c>value</c> = bytes totais
    /// (com fallback para <c>s</c>+<c>r</c> quando ausente).
    /// </summary>
    private static IReadOnlyList<UnifiDpiAppDto> ParseDpiStats(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");

        var apps = new List<UnifiDpiAppDto>();
        foreach (var item in data.EnumerateArray())
        {
            var app = Read(item, "app");
            if (app.Length == 0)
                continue;

            var category = Read(item, "cat");
            if (category.Length == 0)
                category = Read(item, "category");

            var total = ReadLong(item, "value");
            if (total <= 0)
                total = ReadLong(item, "s") + ReadLong(item, "r");

            apps.Add(new UnifiDpiAppDto(app, category, total));
        }

        return apps;
    }

    private static long ReadLong(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt64(out var integer))
                return integer;
            return value.TryGetDouble(out var real) ? (long)real : 0;
        }

        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(value.GetString(), out var parsed))
            return parsed;

        return 0;
    }

    private static DateTime? ReadUnixSeconds(JsonElement element, string property)
    {
        var seconds = ReadLong(element, property);
        return seconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;
    }

    private static string Read(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private enum UniFiApiStyle
    {
        Unknown,
        UniFiOs,
        Standalone
    }
}
