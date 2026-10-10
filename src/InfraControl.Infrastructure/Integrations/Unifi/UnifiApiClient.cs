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
            sites.Add(new UnifiSiteDto(
                Read(item, "short_id"),
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
                LastSeenUnix: ReadLong(item, "last_seen")));
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
            ports.Add(new UnifiSwitchPortDto(
                ReadInt(item, "port_idx") ?? 0,
                FirstNonEmpty(ReadOrNull(item, "name"), $"Port {ReadInt(item, "port_idx") ?? 0}")!,
                LinkUp: item.TryGetProperty("up", out var up) &&
                    up.ValueKind == JsonValueKind.True,
                Speed: ReadOrNull(item, "speed"),
                FullDuplex: item.TryGetProperty("full_duplex", out var fd) &&
                    fd.ValueKind == JsonValueKind.True,
                PortVlan: ReadInt(item, "port_vlan"),
                PoeEnable: item.TryGetProperty("poe_enable", out var pe) &&
                    pe.ValueKind == JsonValueKind.True,
                PoeStatus: ReadOrNull(item, "poe_power"),
                Media: ReadOrNull(item, "media"),
                ClientMac: FirstNonEmpty(
                    ReadOrNull(item, "client_mac"),
                    ReadOrNull(item, "dot1x_v2ope"))));
        }

        return ports;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrEmpty(v));

    private static int? ReadInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;

    private static long? ReadLong(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var number)
            ? number
            : null;

    private static string? ReadOrNull(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()
            : null;

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
