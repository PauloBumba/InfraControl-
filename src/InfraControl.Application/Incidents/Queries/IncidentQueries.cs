using InfraControl.Application.Incidents.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Incidents.Queries;

// F6-T003: leitura de incidentes (GetAlerts da docs/15 é a lista de
// alerts; aqui: Search + Get com vínculos).

public sealed record SearchIncidentsQuery(
    Guid? DeviceId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<IncidentDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchIncidentsQueryHandler(IIncidentRepository repository)
    : IRequestHandler<SearchIncidentsQuery, PagedResult<IncidentDto>>
{
    public async Task<PagedResult<IncidentDto>> Handle(
        SearchIncidentsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var statuses = ParseStatuses(request.Status);

        var result = await repository.SearchAsync(
            request.DeviceId, statuses, page, pageSize, cancellationToken);

        return new PagedResult<IncidentDto>(
            result.Items.Select(item => IncidentDto.From(
                item.Incident, item.AlertCount)).ToArray(),
            result.TotalCount, result.Page, result.PageSize);
    }

    /// <summary>
    /// M7 (caça-bugs): "active" = qualquer estado != Resolved (KPI do
    /// Dashboard precisa do totalCount de abertos — filtrar no front sobre
    /// a 1ª página mentia o número). Nome de enum continua aceito
    /// (case-insensitive) e devolve coleção unitária; null/vazio = sem filtro.
    /// </summary>
    internal static IReadOnlyCollection<IncidentStatus>? ParseStatuses(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var trimmed = status.Trim();
        if (string.Equals(trimmed, "active", StringComparison.OrdinalIgnoreCase))
            return Enum.GetValues<IncidentStatus>()
                .Where(value => value != IncidentStatus.Resolved)
                .ToArray();

        if (!Enum.TryParse(trimmed, ignoreCase: true, out IncidentStatus parsed))
            throw new ArgumentException(
                $"Status inválido: '{status}'. Use active, open, investigating, "
                + "identified, monitoring ou resolved.",
                nameof(status));

        return [parsed];
    }
}

public sealed record GetIncidentQuery(Guid Id)
    : IQuery<IncidentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetIncidentQueryHandler(IIncidentRepository repository)
    : IRequestHandler<GetIncidentQuery, IncidentDto>
{
    public async Task<IncidentDto> Handle(
        GetIncidentQuery request, CancellationToken cancellationToken)
    {
        var incident = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Incidente '{request.Id}' não encontrado.");

        var alerts = await repository.GetAlertsAsync(incident.Id, cancellationToken);

        return IncidentDto.From(
            incident, alerts.Count, alerts.Select(alert => alert.Id).ToList());
    }
}
