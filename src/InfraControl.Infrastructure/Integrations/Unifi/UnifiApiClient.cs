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

    public async Task<IReadOnlyList<UnifiSsidDto>> ListSsidsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => SsidConfigUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseSsids(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<UnifiAccessPointDto>> ListAccessPointsAsync(
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

        return ParseAccessPoints(await response.Content.ReadAsStringAsync(cancellationToken));
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

    public async Task<IReadOnlyList<UnifiSiteDto>> ListSitesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => SelfSitesUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseSites(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<string> GetSystemVersionAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => SysInfoUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseVersion(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<UnifiConnectedClientDto>> ListConnectedClientsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => ClientsStatsUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseConnectedClients(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<UnifiNetworkDto>> ListNetworksAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => NetworkConfUri, reauthenticateOn401: true, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Sessão do UniFi Controller recusada mesmo após re-autenticação.");

        response.EnsureSuccessStatusCode();

        return ParseNetworks(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<UnifiSwitchPortDto>> ListSwitchPortsAsync(
        string deviceMac, CancellationToken cancellationToken = default)
    {
        var compact = (deviceMac ?? string.Empty)
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        if (compact.Length != 12)
            throw new ArgumentException(
                "MAC do device inválido para a consulta de portas.",
                nameof(deviceMac));

        var devices = await ListDevicesAsync(cancellationToken);
        var device = devices.FirstOrDefault(d =>
            d.Mac.Replace(":", string.Empty).Replace("-", string.Empty)
                .Equals(compact, StringComparison.OrdinalIgnoreCase));

        if (device is null)
            throw new KeyNotFoundException(
                $"Device com MAC '{deviceMac}' não foi encontrado no controller.");

        return ParseSwitchPorts(device);
    }


    public async Task<UnifiTopologyDto> GetTopologyAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        using var response = await GetWithSessionAsync(
            http, () => DeviceStatsUri, reauthenticateOn401: true, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("data", out var envelope)
            ? envelope
            : doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
            return new UnifiTopologyDto([], []);

        var nodes = new Dictionary<string, UnifiTopologyNodeDto>(StringComparer.OrdinalIgnoreCase);
        var links = new List<UnifiTopologyLinkDto>();

        // 1) nós = devices conhecidos do controller
        foreach (var item in root.EnumerateArray())
        {
            var mac = Read(item, "mac");
            if (string.IsNullOrEmpty(mac)) continue;
            nodes[mac] = new UnifiTopologyNodeDto(
                NormalizeMac(mac),
                FirstNonEmpty(ReadOrNull(item, "name"), ReadOrNull(item, "model")),
                Read(item, "type") ?? "unknown",
                KindFromType(Read(item, "type")),
                IsKnownDevice: true);
        }

        var known = new HashSet<string>(nodes.Keys.Select(NormalizeMac), StringComparer.OrdinalIgnoreCase);

        void EnsureTarget(string mac)
        {
            var n = NormalizeMac(mac);
            if (!known.Contains(n))
            {
                known.Add(n);
                nodes[mac] = new UnifiTopologyNodeDto(n, null, "unknown", "external", false);
            }
        }

        // 2) links = uplink reportado pelo controller (payload real: {uplink_mac, port_idx, type})
        foreach (var item in root.EnumerateArray())
        {
            var sourceMac = Read(item, "mac");
            if (string.IsNullOrEmpty(sourceMac)) continue;

            if (item.TryGetProperty("uplink", out var uplink) &&
                uplink.ValueKind == JsonValueKind.Object)
            {
                var target = FirstNonEmpty(
                    ReadOrNull(uplink, "uplink_mac"),
                    ReadOrNull(uplink, "mac"));
                if (!string.IsNullOrEmpty(target))
                {
                    var targetIsKnownDevice = known.Contains(NormalizeMac(target));
                    EnsureTarget(target);
                    links.Add(new UnifiTopologyLinkDto(
                        NormalizeMac(sourceMac),
                        FirstNonEmpty(ReadOrNull(item, "name"), ReadOrNull(item, "model")),
                        NormalizeMac(target),
                        nodes.TryGetValue(target, out var tn) ? tn.Name : null,
                        ReadInt(uplink, "port_idx"),
                        FirstNonEmpty(ReadOrNull(uplink, "type"), "wire")!,
                        targetIsKnownDevice ? "confirmed" : "unknown",
                        "controller"));
                }
            }

            // 3) LLDP (quando o controller expõe) — vizinhança, não uplink autoral
            if (item.TryGetProperty("lldp_table", out var lldp) &&
                lldp.ValueKind == JsonValueKind.Array)
            {
                foreach (var neigh in lldp.EnumerateArray())
                {
                    var remote = FirstNonEmpty(
                        ReadOrNull(neigh, "chassis_id"),
                        ReadOrNull(neigh, "ch_id"),
                        ReadOrNull(neigh, "system_mac"));
                    if (string.IsNullOrEmpty(remote)) continue;
                    EnsureTarget(remote);
                    links.Add(new UnifiTopologyLinkDto(
                        NormalizeMac(sourceMac),
                        FirstNonEmpty(ReadOrNull(item, "name"), ReadOrNull(item, "model")),
                        NormalizeMac(remote),
                        FirstNonEmpty(
                            ReadOrNull(neigh, "sys_name"),
                            nodes.TryGetValue(remote, out var rn) ? rn.Name : null),
                        ReadInt(neigh, "port_idx"),
                        "lldp",
                        "inferred",
                        "lldp"));
                }
            }
        }

        return new UnifiTopologyDto(
            nodes.Values.OrderBy(n => n.Mac, StringComparer.OrdinalIgnoreCase).ToList(),
            links);
    }

    private static string NormalizeMac(string mac) =>
        mac.Replace(":", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

    private static string KindFromType(string? type) => type switch
    {
        "ugw" or "uxg" => "gateway",
        "usw" => "switch",
        "uap" => "ap",
        _ => "other",
    };

    public async Task<UnifiPortsSummaryDto> GetPortsSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);
        using var response = await GetWithSessionAsync(
            http, () => DeviceStatsUri, reauthenticateOn401: true, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("data", out var envelope)
            ? envelope
            : doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
            return new UnifiPortsSummaryDto(0, 0);

        var total = 0;
        var up = 0;
        foreach (var device in root.EnumerateArray())
        {
            if (!device.TryGetProperty("port_table", out var ports) ||
                ports.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var port in ports.EnumerateArray())
            {
                total++;
                if (port.TryGetProperty("up", out var linkUp) &&
                    linkUp.ValueKind == JsonValueKind.True)
                    up++;
            }
        }

        return new UnifiPortsSummaryDto(total, up);
    }

    /// <summary>
    /// Fase 07e (ADR-051): grava configuração de porta via GET → modify →
    /// PUT. Nunca POST cego — o device completo é lido, apenas o campo
    /// alvo do porto específico é modificado, e o device é devolvido.
    /// </summary>
    public async Task SetPortConfigAsync(
        string deviceMac,
        int portIndex,
        bool? disabled,
        int? portVlan,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var compact = (deviceMac ?? string.Empty)
            .Replace(":", string.Empty).Replace("-", string.Empty)
            .ToLowerInvariant();
        if (compact.Length != 12)
            throw new ArgumentException(
                "MAC do device inválido para gravação de porta.", nameof(deviceMac));

        var http = _factory.CreateClient(HttpClientName);

        // 1. GET device atual (com port_table completo)
        using var getResponse = await GetWithSessionAsync(
            http, () => DeviceStatsUri, reauthenticateOn401: true, cancellationToken);
        var json = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("data", out var envelope)
            ? envelope
            : doc.RootElement;

        JsonElement? targetDevice = null;
        JsonElement? targetPort = null;
        string? deviceId = null;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var device in root.EnumerateArray())
            {
                var mac = ReadOrNull(device, "mac")?
                    .Replace(":", "").Replace("-", "").ToLowerInvariant();
                if (mac != compact) continue;

                targetDevice = device.Clone();
                deviceId = ReadOrNull(device, "_id");

                if (device.TryGetProperty("port_table", out var ports) &&
                    ports.ValueKind == JsonValueKind.Array)
                {
                    foreach (var port in ports.EnumerateArray())
                    {
                        if (ReadInt(port, "port_idx") == portIndex)
                        {
                            targetPort = port.Clone();
                            break;
                        }
                    }
                }
                break;
            }
        }

        if (targetDevice is null || deviceId is null)
            throw new KeyNotFoundException(
                $"Device com MAC '{deviceMac}' não foi encontrado no controller.");

        if (targetPort is null)
            throw new InvalidOperationException(
                $"Porta {portIndex} não existe no device '{deviceMac}'.");

        // Proteção ADR-051: recusa porta de management
        var isManagement = ReadBool(targetPort.Value, "is_management_port") ||
            (targetDevice.Value.TryGetProperty("ip", out var deviceIp) &&
             deviceIp.ValueKind == JsonValueKind.String &&
             targetPort.Value.TryGetProperty("ip", out var portIp) &&
             portIp.ValueKind == JsonValueKind.String &&
             deviceIp.GetString() == portIp.GetString());

        if (isManagement)
            throw new InvalidOperationException(
                $"A porta {portIndex} é a porta de management do device — operação recusada (ADR-051).");

        // 2. Montar body: device completo com apenas o porto modificado.
        // Usa JsonNode para mutação segura.
        var deviceNode = System.Text.Json.Nodes.JsonNode.Parse(
            targetDevice.Value.GetRawText())!.AsObject();

        if (deviceNode["port_table"] is System.Text.Json.Nodes.JsonArray portArray)
        {
            foreach (var node in portArray)
            {
                if (node is not System.Text.Json.Nodes.JsonObject portObj) continue;
                if (portObj["port_idx"]?.GetValue<int>() != portIndex) continue;

                if (disabled is not null)
                    portObj["disabled"] = disabled.Value;

                if (portVlan is not null)
                {
                    portObj["port_vlan"] = portVlan.Value;
                    portObj["port_vlan_enabled"] = true;
                }
                break;
            }
        }

        var body = new
        {
            port_table = deviceNode["port_table"]
        };

        // 3. PUT device de volta
        var putUri = $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/rest/device/{deviceId}";
        using var putRequest = new HttpRequestMessage(HttpMethod.Put, putUri)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8, "application/json")
        };
        if (_sessionCookie is not null)
            putRequest.Headers.TryAddWithoutValidation("Cookie", _sessionCookie);

        using var putResponse = await http.SendAsync(putRequest, cancellationToken);
        if (!putResponse.IsSuccessStatusCode)
        {
            var error = await putResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Controller UniFi recusou a gravação da porta {portIndex} " +
                $"(HTTP {(int)putResponse.StatusCode}).");
        }
    }

    private string SelfUri => $"{BaseUrl}/api/self";

    private string ClientsStatsUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/sta";

    private string NetworkConfUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/rest/networkconf";

    private string SelfSitesUri => $"{BaseUrl}{ApiPrefix}/api/self/sites";

    private string SysInfoUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/sysinfo";

    private string DeviceStatsUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/device";

    /// <summary>Clientes ativos com contadores rx/tx (F18-T007 · Insights).</summary>
    private string ClientStatsUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/sta";

    /// <summary>DPI por aplicação (F18-T007 · Insights).</summary>
    private string DpiStatsUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/stat/app";

    /// <summary>Configuração das redes SSID (F18-T006 · WiFi).</summary>
    private string SsidConfigUri
        => $"{BaseUrl}{ApiPrefix}/api/s/{_options.Site}/rest/ssidconf";

    /// <summary>Prefixo da API: UniFi OS usa <c>/proxy/network</c>, standalone usa a raiz.</summary>
    private string ApiPrefix
        => _apiStyle == UniFiApiStyle.UniFiOs ? "/proxy/network" : string.Empty;

    private string BaseUrl => _options.BaseUrl.TrimEnd('/');

    private void ApplyPortTables(Dictionary<string, JsonElement> portTables)
    {
        foreach (var (mac, table) in portTables)
            _devicePortTables[mac] = table;
    }

    private IReadOnlyList<UnifiDeviceDto> ParseDevices(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");

        var devices = new List<UnifiDeviceDto>();
        var portTables = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data.EnumerateArray())
        {
            var mac = Read(item, "mac");
            if (item.TryGetProperty("port_table", out var portTable) &&
                !string.IsNullOrEmpty(mac))
                portTables[mac] = portTable.Clone();

            devices.Add(new UnifiDeviceDto(
                Read(item, "mac"),
                Read(item, "name"),
                Read(item, "model"),
                Read(item, "type"),
                Read(item, "ip"),
                SiteId: ReadOrNull(item, "site_id"),
                State: ReadOrNull(item, "state"),
                Firmware: ReadOrNull(item, "version")));
        }

        ApplyPortTables(portTables);
        return devices;
    }

    private static IReadOnlyList<UnifiSiteDto> ParseSites(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");
        var sites = new List<UnifiSiteDto>();
        foreach (var item in data.EnumerateArray())
        {
            // UniFi 10.x omite short_id em /api/self/sites — o próprio name
            // É o identificador curto (ex.: ta262mxg); sem isso o sync grava 0 sites.
            var shortId = Read(item, "short_id");
            if (string.IsNullOrWhiteSpace(shortId))
                shortId = Read(item, "name");
            sites.Add(new UnifiSiteDto(
                shortId ?? string.Empty,
                Read(item, "name"),
                Read(item, "desc"),
                item.TryGetProperty("attr_hidden", out var hidden) &&
                    hidden.ValueKind == JsonValueKind.True));
        }

        return sites;
    }

    private static string ParseVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("version", out var version) &&
                version.ValueKind == JsonValueKind.String)
                return version.GetString() ?? string.Empty;

            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Array &&
                data.GetArrayLength() > 0)
            {
                var first = data[0];
                if (first.ValueKind == JsonValueKind.Object &&
                    first.TryGetProperty("version", out var inner) &&
                    inner.ValueKind == JsonValueKind.String)
                    return inner.GetString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException(
            "Resposta inesperada do UniFi Controller (sem a versão em sysinfo).");
    }

    private static IReadOnlyList<UnifiConnectedClientDto> ParseConnectedClients(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");

        var clients = new List<UnifiConnectedClientDto>();
        foreach (var item in data.EnumerateArray())
        {
            clients.Add(new UnifiConnectedClientDto(
                Read(item, "mac"),
                Ip: ReadOrNull(item, "ip"),
                Hostname: FirstNonEmpty(Read(item, "hostname"), Read(item, "name"), Read(item, "oui")),
                ApMac: ReadOrNull(item, "ap_mac"),
                SwitchMac: ReadOrNull(item, "sw_mac"),
                Essid: FirstNonEmpty(ReadOrNull(item, "essid"), ReadOrNull(item, "network_name")),
                NetworkName: FirstNonEmpty(ReadOrNull(item, "reco_name"), ReadOrNull(item, "network")),
                Signal: ReadInt(item, "signal"),
                IsWired: item.TryGetProperty("is_wired", out var wired) &&
                    wired.ValueKind == JsonValueKind.True,
                LastSeenUnix: ReadLong(item, "last_seen"),
                Radio: ReadOrNull(item, "radio")));
        }

        return clients;
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

    private static IReadOnlyList<UnifiNetworkDto> ParseNetworks(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");
        var networks = new List<UnifiNetworkDto>();
        foreach (var item in data.EnumerateArray())
        {
            networks.Add(new UnifiNetworkDto(
                Read(item, "_id"),
                Read(item, "name"),
                Read(item, "purpose"),
                VlanId: ReadInt(item, "vlan"),
                Enabled: !item.TryGetProperty("enabled", out var enabled) ||
                    enabled.ValueKind != JsonValueKind.False,
                SiteId: ReadOrNull(item, "site_id")));
        }

        return networks;
    }

    /// <summary>
    /// Portas vêm embutidas no item do device (port_table). O DTO de
    /// device não carrega o array cru (REGRA 8 — só cadastro); aqui a
    /// leitura é feita do JSON original guardado no parse.
    /// </summary>
    private readonly Dictionary<string, JsonElement> _devicePortTables =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<UnifiSwitchPortDto> ParseSwitchPorts(UnifiDeviceDto device)
        => _devicePortTables.TryGetValue(device.Mac, out var table)
            ? ParsePortTable(table)
            : [];

    private static IReadOnlyList<UnifiSwitchPortDto> ParsePortTable(JsonElement table)
    {
        var ports = new List<UnifiSwitchPortDto>();
        if (table.ValueKind != JsonValueKind.Array)
            return ports;

        foreach (var item in table.EnumerateArray())
        {
            var idx = ReadInt(item, "port_idx") ?? 0;
            var poeEnable = item.TryGetProperty("poe_enable", out var pe) &&
                pe.ValueKind == JsonValueKind.True;

            // poe_power pode ser watts (float/string numérico) OU um status
            // cru do controller (ex.: "RUNNING"). Preservar string não-numérica;
            // para numérico, derivar: enable+power>0 = delivering; enable+0 = on.
            var poePowerRaw = ReadOrNull(item, "poe_power");
            var poePower = ReadFloatOrNull(item, "poe_power");
            string? poeStatus;
            if (poePowerRaw is not null && poePower is null)
            {
                // String não-numérica — status cru do controller.
                poeStatus = poePowerRaw;
            }
            else
            {
                poeStatus = !poeEnable
                    ? "none"
                    : poePower is > 0 ? "delivering" : "on";
            }

            // speed chega como int (10/100/1000) ou string — normalizar.
            var speed = ReadSpeed(item);

            ports.Add(new UnifiSwitchPortDto(
                idx,
                FirstNonEmpty(ReadOrNull(item, "name"), $"Port {idx}")!,
                LinkUp: item.TryGetProperty("up", out var up) &&
                    up.ValueKind == JsonValueKind.True,
                Speed: speed,
                // F30-T001 S4a: ausente ⇒ null (nunca false — isso inventava
                // meio-duplex quando o controller não manda "full_duplex").
                FullDuplex: item.TryGetProperty("full_duplex", out var fd)
                    ? fd.ValueKind switch
                    {
                        JsonValueKind.True => (bool?)true,
                        JsonValueKind.False => (bool?)false,
                        _ => null,
                    }
                    : null,
                PortVlan: ReadInt(item, "port_vlan"),
                PoeEnable: poeEnable,
                PoeStatus: poeStatus,
                Media: ReadOrNull(item, "media"),
                ClientMac: FirstNonEmpty(
                    ReadOrNull(item, "client_mac"),
                    ReadOrNull(item, "dot1x_v2ope")),
                // ADR-061 §3 (H6): estado administrativo — port_conf_enabled
                // (port_table) com fallback "enabled"; ausente ⇒ true.
                Enabled: item.TryGetProperty("port_conf_enabled", out var pce)
                    ? pce.ValueKind == JsonValueKind.True
                    : !item.TryGetProperty("enabled", out var pen) ||
                        pen.ValueKind == JsonValueKind.True));
        }

        return ports;
    }

    private static float? ReadFloatOrNull(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var f))
            return f;
        if (value.ValueKind == JsonValueKind.String &&
            float.TryParse(value.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static string? ReadSpeed(JsonElement element)
    {
        // Controller devolve int (10/100/1000) ou string ("1G"/"100M").
        if (element.TryGetProperty("speed", out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
                return n switch
                {
                    0 => null, // porta down — controller reporta 0
                    10 => "10M",
                    100 => "100M",
                    1000 => "1G",
                    _ => $"{n}M"
                };
            if (value.ValueKind == JsonValueKind.String)
            {
                var s = value.GetString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrEmpty(v));

    private static int? ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        // TryGetInt32 LANÇA quando o ValueKind é String (controller real
        // devolve "vlan":"21") — tratar número-vindo-como-string.
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }


    private static string? ReadOrNull(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()
            : null;

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

    /// <summary>
    /// F18-T006: <c>GET /rest/ssidconf</c> devolve a CONFIGURAÇÃO de cada
    /// rede — <c>security</c> bruta (wpapsk/wpa2psk/wpa3/none…), <c>vlan</c>
    /// (0/ausente = sem VLAN), <c>is_guest</c> e os flags de banda
    /// <c>no2ghz</c>/<c>no5ghz</c> (ausentes = Controller não informou e a
    /// banda fica vazia na UI — nunca suposta).
    /// </summary>
    private static IReadOnlyList<UnifiSsidDto> ParseSsids(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");

        var ssids = new List<UnifiSsidDto>();
        foreach (var item in data.EnumerateArray())
        {
            var name = Read(item, "name");
            if (name.Length == 0)
                continue;

            ssids.Add(new UnifiSsidDto(
                name,
                Read(item, "security"),
                ReadBool(item, "enabled"),
                (int)ReadLong(item, "vlan"),
                ResolveBands(item),
                ReadBool(item, "is_guest")));
        }

        return ssids;
    }

    /// <summary>
    /// Deriva as bandas de <c>no2ghz</c>/<c>no5ghz</c>: o flag é "não
    /// habilita X GHz" (true = X desligada). Só bandas com flag conhecido
    /// e habilitado entram; sem nenhum flag → vazio (não informado).
    /// </summary>
    private static string ResolveBands(JsonElement item)
    {
        var two = ReadBoolOrNull(item, "no2ghz");
        var five = ReadBoolOrNull(item, "no5ghz");

        var bands = new List<string>();
        if (two == false)
            bands.Add("2.4 GHz");
        if (five == false)
            bands.Add("5 GHz");

        return string.Join(" + ", bands);
    }

    /// <summary>
    /// F18-T006: <c>GET /stat/device</c> com telemetria — <c>state</c>
    /// (1 = conectado), <c>uptime</c> em segundos, <c>system-stats</c>
    /// (cpu/mem em string percentual), contadores rx/tx, clientes somados
    /// de <c>vap_table</c> e os rádios de <c>radio_table</c>. Campos ausentes
    /// viram 0/vazio — valor nunca é inventado.
    /// </summary>
    private static IReadOnlyList<UnifiAccessPointDto> ParseAccessPoints(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Resposta inesperada do UniFi Controller (sem o array 'data').");

        var accessPoints = new List<UnifiAccessPointDto>();
        foreach (var item in data.EnumerateArray())
        {
            var cpu = 0;
            var mem = 0;
            if (item.TryGetProperty("system-stats", out var stats) &&
                stats.ValueKind == JsonValueKind.Object)
            {
                cpu = ReadPercent(stats, "cpu");
                mem = ReadPercent(stats, "mem");
            }

            accessPoints.Add(new UnifiAccessPointDto(
                Read(item, "mac"),
                Read(item, "name"),
                Read(item, "model"),
                Read(item, "ip"),
                ReadLong(item, "state") == 1,
                ReadLong(item, "uptime"),
                cpu,
                mem,
                ReadLong(item, "rx_bytes"),
                ReadLong(item, "tx_bytes"),
                CountStationEntries(item),
                ParseRadios(item)));
        }

        return accessPoints;
    }

    /// <summary>Clientes conectados ao AP = soma dos <c>num_sta</c> de cada
    /// entrada de <c>vap_table</c> (uma por SSID/rádio).</summary>
    private static int CountStationEntries(JsonElement device)
    {
        if (!device.TryGetProperty("vap_table", out var table) ||
            table.ValueKind != JsonValueKind.Array)
            return 0;

        var count = 0;
        foreach (var entry in table.EnumerateArray())
            count += (int)ReadLong(entry, "num_sta");
        return count;
    }

    private static IReadOnlyList<UnifiRadioDto> ParseRadios(JsonElement device)
    {
        if (!device.TryGetProperty("radio_table", out var table) ||
            table.ValueKind != JsonValueKind.Array)
            return [];

        var radios = new List<UnifiRadioDto>();
        foreach (var radio in table.EnumerateArray())
        {
            var band = Read(radio, "band");
            if (band.Length == 0)
                continue;

            radios.Add(new UnifiRadioDto(
                band,
                (int)ReadLong(radio, "channel"),
                (int)ReadLong(radio, "width"),
                (int)ReadLong(radio, "tx_power"),
                (int)ReadLong(radio, "utilization")));
        }

        return radios;
    }

    private static int ReadPercent(JsonElement element, string property)
    {
        var raw = element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.String when double.TryParse(
                    value.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => 0
            }
            : 0;

        return raw <= 0 ? 0 : (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    private static bool ReadBool(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.True;

    private static bool? ReadBoolOrNull(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) &&
           value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

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
