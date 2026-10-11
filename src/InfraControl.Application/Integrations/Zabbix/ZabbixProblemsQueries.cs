using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Zabbix;

// F27 Fase 10.1/10.2/10.3 (backend): consultas de problemas ativos,
// histórico sob demanda e resumo do dashboard — todas somente LEITURA na
// API do Zabbix (§11.1). Seguem o padrão da casa: BaseQuery + 
// IAuthorizedRequest (RoleAccess.Read) com cache curto via CachingBehavior.

/// <summary>Visão de um problema para a UI (F27 Fase 10.1).</summary>
public sealed record ZabbixProblemViewDto(
    string EventId,
    string Name,
    int Severity,
    string SeverityLabel,
    DateTime ClockUtc,
    bool Recovered,
    IReadOnlyList<string> HostNames)
{
    /// <summary>Severidades do Zabbix 0..5 (Not classified..Disaster).</summary>
    public static string SeverityLabelOf(int severity) => severity switch
    {
        0 => "Não classificado",
        1 => "Informativo",
        2 => "Aviso",
        3 => "Médio",
        4 => "Alto",
        5 => "Desastre",
        _ => $"Severidade {severity}"
    };
}

public sealed record ZabbixProblemsResultDto(
    IReadOnlyList<ZabbixProblemViewDto> Items,
    int Total);

/// <summary>
/// F27 Fase 10.1: consulta os problemas ativos do Zabbix (problem.get
/// recent, ao vivo — o read-model local não replica problemas; o webhook
/// da F16 já ingere PROBLEM/OK em Incidentes quando configurado).
/// MinSeverity filtra 0..5; IncludeRecovered=false (default) esconde os
/// que já têm evento de recuperação.
/// </summary>
public sealed record GetZabbixProblemsQuery(
    int? MinSeverity = null,
    bool IncludeRecovered = false)
    : BaseQuery<ZabbixProblemsResultDto>, IAuthorizedRequest
{
    public override string CacheKey =>
        $"{GetType().Name}:{MinSeverity?.ToString() ?? "any"}:{IncludeRecovered}";

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetZabbixProblemsQueryHandler(IZabbixClient client)
    : IRequestHandler<GetZabbixProblemsQuery, ZabbixProblemsResultDto>
{
    public async Task<ZabbixProblemsResultDto> Handle(
        GetZabbixProblemsQuery request, CancellationToken cancellationToken)
    {
        if (request.MinSeverity is < 0 or > 5)
            throw new ArgumentException(
                "A severidade mínima deve estar entre 0 (Não classificado) e 5 (Desastre).",
                nameof(request.MinSeverity));

        var problems = await client.ListProblemsAsync(cancellationToken);

        IEnumerable<ZabbixProblemDto> filtered = problems;

        if (request.MinSeverity is { } min)
            filtered = filtered.Where(p => p.Severity >= min);

        if (!request.IncludeRecovered)
            filtered = filtered.Where(p => p.RecoveryEventId is null);

        var items = filtered
            .OrderByDescending(p => p.Severity)
            .ThenByDescending(p => p.ClockUtc)
            .Select(p => new ZabbixProblemViewDto(
                p.EventId,
                p.Name,
                p.Severity,
                ZabbixProblemViewDto.SeverityLabelOf(p.Severity),
                p.ClockUtc,
                Recovered: p.RecoveryEventId is not null,
                p.HostNames ?? []))
            .ToList();

        return new ZabbixProblemsResultDto(items, items.Count);
    }
}

/// <summary>Ponto do histórico de um item (F27 Fase 10.2).</summary>
public sealed record ZabbixHistoryPointDto(DateTime ClockUtc, string Value);

/// <summary>
/// F27 Fase 10.2: histórico de métricas de um item sob demanda
/// (history.get). Limites: itemId obrigatório, período válido, no máximo
/// 92 dias por consulta e 1..100000 pontos (o cliente também valida).
/// </summary>
public sealed record GetZabbixHistoryQuery(
    string ItemId,
    DateTime FromUtc,
    DateTime ToUtc,
    int Limit = 500)
    : BaseQuery<IReadOnlyList<ZabbixHistoryPointDto>>, IAuthorizedRequest
{
    public override string CacheKey =>
        $"{GetType().Name}:{ItemId}:{FromUtc:O}:{ToUtc:O}:{Limit}";

    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetZabbixHistoryQueryHandler(IZabbixClient client)
    : IRequestHandler<GetZabbixHistoryQuery, IReadOnlyList<ZabbixHistoryPointDto>>
{
    internal const int MaxRangeDays = 92;

    public async Task<IReadOnlyList<ZabbixHistoryPointDto>> Handle(
        GetZabbixHistoryQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
            throw new ArgumentException(
                "O itemId é obrigatório.", nameof(request.ItemId));

        if (request.ToUtc < request.FromUtc)
            throw new ArgumentException(
                "O período final deve ser maior ou igual ao inicial.",
                nameof(request.ToUtc));

        if (request.ToUtc - request.FromUtc > TimeSpan.FromDays(MaxRangeDays))
            throw new ArgumentException(
                $"O período máximo por consulta é de {MaxRangeDays} dias.",
                nameof(request.FromUtc));

        if (request.Limit is < 1 or > 100000)
            throw new ArgumentException(
                "O limite deve estar entre 1 e 100000.", nameof(request.Limit));

        var values = await client.GetHistoryAsync(
            request.ItemId, request.FromUtc, request.ToUtc,
            request.Limit, cancellationToken);

        return values
            .Select(v => new ZabbixHistoryPointDto(v.ClockUtc, v.Value))
            .ToList();
    }
}

/// <summary>
/// F27 Fase 10.3: resumo do dashboard de infraestrutura — contagens do
/// read-model local (hosts), match com Assets, estado da integração e
/// distribuição de problemas por severidade (consulta ao vivo; falha vira
/// <see cref="ProblemsAvailable"/>=false + mensagem fixa — nunca segredo).
/// </summary>
public sealed record ZabbixSummaryDto(
    int TotalHosts,
    int EnabledHosts,
    int MissingHosts,
    int HostsWithAsset,
    int HostsWithoutAsset,
    int ProblemsTotal,
    IReadOnlyList<int> ProblemsBySeverity,
    int HostsWithProblems,
    bool ProblemsAvailable,
    string? ProblemsError,
    bool IsConfigured,
    string? DetectedVersion,
    DateTime? LastInventorySyncAtUtc);

public sealed record GetZabbixSummaryQuery
    : BaseQuery<ZabbixSummaryDto>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetZabbixSummaryQueryHandler(
    IZabbixClient client,
    IZabbixHostsRepository hostsRepository,
    IZabbixSettingsRepository settingsRepository,
    IDeviceRepository deviceRepository,
    ILogger<GetZabbixSummaryQueryHandler> logger)
    : IRequestHandler<GetZabbixSummaryQuery, ZabbixSummaryDto>
{
    public async Task<ZabbixSummaryDto> Handle(
        GetZabbixSummaryQuery request, CancellationToken cancellationToken)
    {
        var counts = await hostsRepository.GetCountsAsync(cancellationToken);
        var hostsWithAsset = await CountHostsWithAssetAsync(cancellationToken);

        var setting = await settingsRepository.GetAsync(cancellationToken);
        var isConfigured = setting is not null
            && !string.IsNullOrWhiteSpace(setting.ApiUrl)
            && !string.IsNullOrWhiteSpace(setting.Username);

        var bySeverity = new int[6];
        var problemsTotal = 0;
        var hostsWithProblems = 0;
        var problemsAvailable = false;
        string? problemsError = null;

        if (client.IsConfigured)
        {
            try
            {
                var problems = await client.ListProblemsAsync(cancellationToken);
                problemsAvailable = true;
                problemsTotal = problems.Count;

                foreach (var problem in problems)
                    bySeverity[Math.Clamp(problem.Severity, 0, 5)]++;

                hostsWithProblems = problems
                    .SelectMany(p => p.HostNames ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .Count();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Falha ao consultar problemas Zabbix para o resumo do dashboard");
                problemsError = "Não foi possível consultar os problemas no Zabbix.";
            }
        }
        else
        {
            problemsError = "Integração com Zabbix não configurada.";
        }

        return new ZabbixSummaryDto(
            counts.Total,
            counts.Enabled,
            counts.Missing,
            hostsWithAsset,
            counts.Total - hostsWithAsset,
            problemsTotal,
            bySeverity,
            hostsWithProblems,
            problemsAvailable,
            problemsError,
            isConfigured,
            setting?.DetectedVersion,
            setting?.LastInventorySyncAtUtc);
    }

    /// <summary>
    /// Match por IP exato com fallback por hostname (mesmo critério do
    /// IngestZabbixProblemCommand e do GetZabbixHostsQuery — F4-T010);
    /// uma única carga de Devices evita N consultas.
    /// </summary>
    private async Task<int> CountHostsWithAssetAsync(
        CancellationToken cancellationToken)
    {
        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);

        var ips = new HashSet<string>(
            devices.Select(d => d.Ip).Where(ip => !string.IsNullOrWhiteSpace(ip)),
            StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(
            devices.Select(d => d.Name).Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.Ordinal);

        var hosts = await hostsRepository.ListAsync(cancellationToken);
        return hosts.Count(h =>
            (!string.IsNullOrWhiteSpace(h.Ip) && ips.Contains(h.Ip))
            || names.Contains(h.Host));
    }
}
