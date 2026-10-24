using System.Text;
using System.Text.Json;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Integrations.Zabbix.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Integrations.Zabbix;

/// <summary>
/// F27 Fase 2.1: cliente JSON-RPC 2.0 para a API do Zabbix. Autenticação
/// por <c>user.login</c> com cache de sessão (uma única chamada de login
/// por vida do cliente); a F27 §1.2.1 confirmou que o Zabbix 8.0 rejeita o
/// campo <c>auth</c> no corpo e exige <c>Authorization: Bearer</c> — a
/// detecção é pela versão (<c>apiinfo.version</c>, sem auth): major ≥ 8
/// usa Bearer, ≤ 7 usa o campo <c>auth</c>. Sessão expirada (erro
/// JSON-RPC de autorização) dispara re-login e uma nova tentativa.
/// Falhas HTTP, erros JSON-RPC e respostas inválidas viram exceção com
/// mensagem identificável — <b>nunca</b> com token ou senha (nem em log).
/// Resiliência (retry/backoff/circuit breaker) fica no named client
/// <c>zabbix-api</c> (política do dispatcher n8n — ADR-028 §6).
/// </summary>
public sealed class ZabbixApiClient : IZabbixClient
{
    public const string HttpClientName = "zabbix-api";

    internal const int DefaultListLimit = 1000;
    internal const int MaxHistoryLimit = 100000;

    private readonly IHttpClientFactory _factory;
    private readonly ZabbixIntegrationOptions _options;
    private readonly ILogger<ZabbixApiClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _sessionToken;
    private int? _majorVersion;

    public ZabbixApiClient(
        IHttpClientFactory factory,
        IOptions<ZabbixIntegrationOptions> options,
        ILogger<ZabbixApiClient> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsApiConfigured;

    public async Task<string> GetVersionAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);

        // apiinfo.version não exige autenticação (nem no 8.0).
        var result = await CallAsync(
            http, "apiinfo.version", new { }, authenticated: false,
            allowAuthRetry: false, cancellationToken);

        var version = result.GetString();
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException(
                "Resposta inesperada da API do Zabbix (apiinfo.version sem valor).");

        return version;
    }

    public async Task<IReadOnlyList<ZabbixHostDto>> ListHostsAsync(
        CancellationToken cancellationToken = default)
    {
        // Zabbix 7.2+ renomeou o objeto de "groups" para "hostgroups" e o
        // parâmetro selectGroups → selectHostGroups; o 8.0 ignora o select
        // antigo silenciosamente (sem erro, sem dados). Pedimos os dois e
        // unimos os arrays — em cada versão só um vem preenchido.
        var result = await CallListAsync("host.get", new
        {
            output = new[] { "hostid", "host", "name", "status", "flags" },
            selectInterfaces = new[] { "ip", "port", "type", "main" },
            selectParentTemplates = new[] { "templateid" },
            selectGroups = new[] { "groupid" },
            selectHostGroups = new[] { "groupid" },
            limit = DefaultListLimit
        }, cancellationToken);

        return result.EnumerateArray()
            .Select(host => new ZabbixHostDto(
                Read(host, "hostid"),
                Read(host, "host"),
                Read(host, "name"),
                Enabled: Read(host, "status") != "1",
                Discovered: Read(host, "flags") == "2",
                Interfaces: ReadArray(host, "interfaces")
                    .Select(iface => new ZabbixInterfaceDto(
                        Read(iface, "ip"),
                        ReadInt(iface, "port"),
                        ReadInt(iface, "type"),
                        Main: Read(iface, "main") == "1"))
                    .ToList(),
                GroupIds: ReadArray(host, "hostgroups")
                    .Concat(ReadArray(host, "groups"))
                    .Select(group => Read(group, "groupid"))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                TemplateIds: ReadArray(host, "parentTemplates")
                    .Select(template => Read(template, "templateid"))
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ZabbixHostGroupDto>> ListHostGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallListAsync("hostgroup.get", new
        {
            output = new[] { "groupid", "name" },
            limit = DefaultListLimit
        }, cancellationToken);

        return result.EnumerateArray()
            .Select(group => new ZabbixHostGroupDto(
                Read(group, "groupid"),
                Read(group, "name")))
            .ToList();
    }

    public async Task<IReadOnlyList<ZabbixTemplateDto>> ListTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallListAsync("template.get", new
        {
            output = new[] { "templateid", "name" },
            limit = DefaultListLimit
        }, cancellationToken);

        return result.EnumerateArray()
            .Select(template => new ZabbixTemplateDto(
                Read(template, "templateid"),
                Read(template, "name")))
            .ToList();
    }

    public async Task<IReadOnlyList<ZabbixItemDto>> ListItemsAsync(
        string hostId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostId))
            throw new ArgumentException(
                "O hostid é obrigatório para listar itens.", nameof(hostId));

        var result = await CallListAsync("item.get", new
        {
            output = new[] { "itemid", "hostid", "name", "key_", "type", "state", "status", "units" },
            hostids = hostId,
            limit = DefaultListLimit
        }, cancellationToken);

        return result.EnumerateArray()
            .Select(item => new ZabbixItemDto(
                Read(item, "itemid"),
                Read(item, "hostid"),
                Read(item, "name"),
                Read(item, "key_"),
                ReadInt(item, "type"),
                Enabled: Read(item, "status") != "1",
                Unsupported: Read(item, "state") == "1",
                Unit: Read(item, "units")))
            .ToList();
    }

    public async Task<IReadOnlyList<ZabbixTriggerDto>> ListTriggersAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallListAsync("trigger.get", new
        {
            output = new[] { "triggerid", "description", "priority", "status", "value" },
            only_true = true,
            monitored = true,
            limit = DefaultListLimit
        }, cancellationToken);

        return result.EnumerateArray()
            .Select(trigger => new ZabbixTriggerDto(
                Read(trigger, "triggerid"),
                Read(trigger, "description"),
                ReadInt(trigger, "priority"),
                Enabled: Read(trigger, "status") != "1",
                Value: Read(trigger, "value") == "1"))
            .ToList();
    }

    public async Task<IReadOnlyList<ZabbixProblemDto>> ListProblemsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallListAsync("problem.get", new
        {
            output = new[] { "eventid", "name", "severity", "clock", "r_eventid", "objectid" },
            recent = true,
            limit = DefaultListLimit
        }, cancellationToken);

        var problems = result.EnumerateArray()
            .Select(problem => new ZabbixProblemDto(
                Read(problem, "eventid"),
                Read(problem, "name"),
                ReadInt(problem, "severity"),
                ClockUtc: DateTimeOffset.FromUnixTimeSeconds(ReadLong(problem, "clock"))
                    .UtcDateTime,
                RecoveryEventId: NormalizeRecoveryId(ReadOrNull(problem, "r_eventid")),
                ObjectId: ReadOrNull(problem, "objectid")))
            .ToList();

        if (problems.Count == 0)
            return problems;

        // Hosts do problema (F27 Fase 10.1): o problem.get do Zabbix 8.0
        // REMOVEU selectHosts (confirmado ao vivo: -32602 "unexpected
        // parameter") — o mapeamento é pelo objectid (triggerid) via
        // trigger.get…selectHosts. Falha aqui não derruba a lista: os
        // problemas voltam sem hosts (só o log de warning).
        try
        {
            var triggerIds = problems
                .Select(problem => problem.ObjectId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (triggerIds.Count == 0)
                return problems;

            var triggers = await CallListAsync("trigger.get", new
            {
                output = new[] { "triggerid" },
                triggerids = triggerIds,
                selectHosts = new[] { "hostid", "host", "name" },
                limit = DefaultListLimit
            }, cancellationToken);

            var hostsByTrigger = triggers.EnumerateArray()
                .ToDictionary(
                    trigger => Read(trigger, "triggerid"),
                    trigger => ReadArray(trigger, "hosts")
                        .Select(host => DisplayName(host))
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.Ordinal)
                        .ToList(),
                    StringComparer.Ordinal);

            return problems
                .Select(problem => problem with
                {
                    HostNames = problem.ObjectId is not null
                        && hostsByTrigger.TryGetValue(problem.ObjectId, out var hosts)
                        ? hosts
                        : []
                })
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Falha ao resolver hosts dos problemas Zabbix — retornando sem hosts");
            return problems;
        }

        static string DisplayName(JsonElement host)
        {
            var name = Read(host, "name");
            return string.IsNullOrWhiteSpace(name) ? Read(host, "host") : name;
        }
    }

    public async Task<IReadOnlyList<ZabbixHistoryValueDto>> GetHistoryAsync(
        string itemId,
        DateTime fromUtc,
        DateTime toUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException(
                "O itemid é obrigatório para consultar o histórico.", nameof(itemId));

        if (limit <= 0 || limit > MaxHistoryLimit)
            throw new ArgumentOutOfRangeException(nameof(limit),
                $"O limite do histórico deve estar entre 1 e {MaxHistoryLimit}.");

        if (toUtc < fromUtc)
            throw new ArgumentException(
                "O período final deve ser maior ou igual ao inicial.",
                nameof(toUtc));

        var result = await CallListAsync("history.get", new
        {
            output = new[] { "itemid", "clock", "value" },
            itemids = new[] { itemId },
            time_from = (int)new DateTimeOffset(
                DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            time_till = (int)new DateTimeOffset(
                DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            sortfield = "clock",
            sortorder = "ASC",
            limit = limit
        }, cancellationToken);

        return result.EnumerateArray()
            .Select(value => new ZabbixHistoryValueDto(
                Read(value, "itemid"),
                DateTimeOffset.FromUnixTimeSeconds(ReadLong(value, "clock")).UtcDateTime,
                Read(value, "value")))
            .ToList();
    }

    /// <summary>Chamada autenticada de lista comum (limite já no params).</summary>
    private async Task<JsonElement> CallListAsync(
        string method, object parameters, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var http = _factory.CreateClient(HttpClientName);
        return await CallAsync(
            http, method, parameters, authenticated: true,
            allowAuthRetry: true, cancellationToken);
    }

    private async Task<JsonElement> CallAsync(
        HttpClient http,
        string method,
        object parameters,
        bool authenticated,
        bool allowAuthRetry,
        CancellationToken cancellationToken)
    {
        if (authenticated)
            await EnsureAuthenticatedAsync(http, cancellationToken);

        try
        {
            return await PostRpcAsync(
                http, method, parameters, authenticated, cancellationToken);
        }
        catch (ZabbixAuthException) when (allowAuthRetry)
        {
            // sessão expirada/removida no servidor → re-login + 1 tentativa
            _logger.LogInformation(
                "Sessão Zabbix recusada em {Method} — re-autenticando", method);
            await LoginAsync(http, invalidateSession: true, cancellationToken);
            return await PostRpcAsync(
                http, method, parameters, authenticated, cancellationToken);
        }
    }

    private async Task<JsonElement> PostRpcAsync(
        HttpClient http,
        string method,
        object parameters,
        bool authenticated,
        CancellationToken cancellationToken)
    {
        var major = authenticated
            ? await EnsureMajorVersionAsync(http, cancellationToken)
            : (int?)null;

        var body = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters,
            ["id"] = 1
        };

        // Zabbix ≥ 8: Authorization: Bearer (o campo auth é rejeitado).
        // Zabbix ≤ 7: campo auth no corpo.
        var request = new HttpRequestMessage(HttpMethod.Post, ApiUri);
        if (authenticated)
        {
            if (major >= 8)
                request.Headers.TryAddWithoutValidation(
                    "Authorization", $"Bearer {_sessionToken}");
            else
                body["auth"] = _sessionToken!;
        }

        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"API do Zabbix respondeu HTTP {(int)response.StatusCode} em {method}.");

        return ParseResult(method, payload);
    }

    private static JsonElement ParseResult(string method, string payload)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"Resposta inválida da API do Zabbix em {method} (JSON malformado).");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("code", out var codeElement)
                    ? codeElement.GetInt32()
                    : 0;
                var message = error.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString() ?? "erro desconhecido"
                    : "erro desconhecido";

                if (IsAuthError(code, message))
                    throw new ZabbixAuthException(code, message);

                // o campo "data" do erro pode repetir a mensagem — nunca token/senha
                var data = error.TryGetProperty("data", out var dataElement)
                    ? dataElement.GetString()
                    : null;
                var detail = string.IsNullOrWhiteSpace(data) ? message : $"{message}: {data}";
                throw new InvalidOperationException(
                    $"Erro JSON-RPC do Zabbix em {method} (código {code}): {detail}");
            }

            if (!root.TryGetProperty("result", out var result))
                throw new InvalidOperationException(
                    $"Resposta inesperada da API do Zabbix em {method} (sem 'result').");

            return result.Clone();
        }
    }

    private static bool IsAuthError(int code, string message) =>
        message.Contains("Not authorized", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Not authorised", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Session terminated", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("re-login", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Invalid auth", StringComparison.OrdinalIgnoreCase) ||
        (code == -32602 && message.Contains("Session", StringComparison.OrdinalIgnoreCase));

    private async Task EnsureAuthenticatedAsync(
        HttpClient http, CancellationToken cancellationToken)
    {
        if (_sessionToken is not null)
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
                _sessionToken = null;

            if (_sessionToken is not null)
                return;

            var result = await PostRpcAsync(
                http, "user.login",
                new { username = _options.Username, password = _options.Password },
                authenticated: false, cancellationToken);

            var token = result.GetString();
            if (string.IsNullOrWhiteSpace(token))
                throw new UnauthorizedAccessException(
                    "Zabbix não devolveu token de sessão em user.login.");

            _sessionToken = token;
            _logger.LogDebug("Sessão Zabbix obtida");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<int> EnsureMajorVersionAsync(
        HttpClient http, CancellationToken cancellationToken)
    {
        if (_majorVersion is not null)
            return _majorVersion.Value;

        var result = await PostRpcAsync(
            http, "apiinfo.version", new { }, authenticated: false, cancellationToken);

        var version = result.GetString() ?? string.Empty;
        var major = int.TryParse(version.Split('.')[0], out var parsed) ? parsed : 0;
        _majorVersion = major;
        return major;
    }

    private void EnsureConfigured()
    {
        if (!_options.IsApiConfigured)
            throw new InvalidOperationException(
                "Integração Zabbix não configurada (Integrations:Zabbix:ApiUrl/Username/Password).");
    }

    private string ApiUri
    {
        get
        {
            var baseUrl = _options.ApiUrl.TrimEnd('/');
            return baseUrl.EndsWith("/api_jsonrpc.php", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : $"{baseUrl}/api_jsonrpc.php";
        }
    }

    private static string Read(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : element.TryGetProperty(property, out var raw)
                ? raw.ToString()
                : string.Empty;

    private static string? ReadOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrEmpty(value.GetString())
            ? value.GetString()
            : null;

    /// <summary>
    /// <c>r_eventid</c> vem como <c>"0"</c> enquanto o problema não tem
    /// evento de recuperação (F27 Fase 10.1) — normaliza para null.
    /// </summary>
    private static string? NormalizeRecoveryId(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "0" ? null : value;

    private static int ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : int.TryParse(Read(element, property), out var parsed)
                ? parsed
                : 0;

    private static long ReadLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var number)
            ? number
            : long.TryParse(Read(element, property), out var parsed)
                ? parsed
                : 0;

    private static IEnumerable<JsonElement> ReadArray(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];
}

/// <summary>
/// Erro de autorização da API Zabbix (sessão expirada/recusada) — sinal
/// interno para re-login; nunca vira exceção exposta ao chamador.
/// </summary>
public sealed class ZabbixAuthException(int code, string message)
    : Exception($"Sessão Zabbix recusada (código {code}): {message}")
{
    public int Code { get; } = code;
}
