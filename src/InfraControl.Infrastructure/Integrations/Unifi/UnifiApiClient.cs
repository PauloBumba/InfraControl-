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
/// F20-T001/ADR-028 §2: POST /api/auth/login → cookie de sessão
/// <c>unifises</c>; re-autenticação automática uma vez quando o Controller
/// responde 401 (sessão expirada). Resiliência (retry/circuit breaker) fica
/// no handler do <c>IHttpClientFactory</c> (named client <c>unifi</c>,
/// política reaproveitada do dispatcher de webhook — ADR-028 §6).
/// </summary>
public sealed class UnifiApiClient : IUnifiApiClient
{
    public const string HttpClientName = "unifi";

    private static readonly Regex UnifisesCookie = new(
        @"unifises=([^;]+)", RegexOptions.Compiled);

    private readonly IHttpClientFactory _factory;
    private readonly UnifiIntegrationOptions _options;
    private readonly ILogger<UnifiApiClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _sessionCookie;

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
            http, DeviceStatsUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseDevices(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            return false;

        try
        {
            var http = _factory.CreateClient(HttpClientName);
            using var response = await GetWithSessionAsync(
                http, SelfUri, reauthenticateOn401: true, cancellationToken);
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
        string uri,
        bool reauthenticateOn401,
        CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(http, cancellationToken);

        var response = await SendWithSessionAsync(http, uri, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            reauthenticateOn401)
        {
            response.Dispose();
            await LoginAsync(http, invalidateSession: true, cancellationToken);
            response = await SendWithSessionAsync(http, uri, cancellationToken);
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

            var payload = JsonSerializer.Serialize(
                new { username = _options.Username, password = _options.Password });

            using var response = await http.PostAsync(
                LoginUri,
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException(
                    "Credenciais do UniFi Controller inváladas ou sessão recusada.");

            response.EnsureSuccessStatusCode();

            var cookie = ExtractSessionCookie(response)
                ?? throw new InvalidOperationException(
                    "UniFi Controller não devolveu o cookie de sessão unifises.");

            _sessionCookie = cookie;
            _logger.LogDebug("Sessão UniFi obtida ({Provider})", "Unifi");
        }
        finally
        {
            _gate.Release();
        }
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

    private string LoginUri => $"{BaseUrl}/api/auth/login";

    private string SelfUri => $"{BaseUrl}/api/self";

    private string DeviceStatsUri
        => $"{BaseUrl}/proxy/network/api/s/{_options.Site}/stat/device";

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

    private static string Read(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
