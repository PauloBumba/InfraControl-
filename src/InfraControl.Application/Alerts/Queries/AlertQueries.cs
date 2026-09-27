using InfraControl.Application.Alerts.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts.Queries;

// F6-T001: listagem de alertas (GetAlerts da docs/15). Criação é
// automática pós-check — não há POST público de alerta.

/// <summary>Alertas paginados — status: open|resolved|vazio = todos.</summary>
public sealed record SearchAlertsQuery(
    Guid? DeviceId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<AlertDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.MonitoringRead.ToString()];
}

public sealed class SearchAlertsQueryHandler(IAlertRepository repository)
    : IRequestHandler<SearchAlertsQuery, PagedResult<AlertDto>>
{
    public async Task<PagedResult<AlertDto>> Handle(
        SearchAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var status = ParseStatus(request.Status);

        var result = await repository.SearchAsync(
            request.DeviceId, status, page, pageSize, cancellationToken);

        return new PagedResult<AlertDto>(
            result.Items.Select(item => new AlertDto(
                item.Alert.Id,
                item.Alert.DeviceId,
                item.DeviceName,
                item.Alert.Kind.ToString(),
                item.Alert.Severity.ToString(),
                item.Alert.Status.ToString(),
                item.Alert.Message,
                item.Alert.CreateDateTime,
                item.Alert.ResolvedAt)).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    internal static AlertStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant() switch
        {
            "open" => AlertStatus.Open,
            "resolved" => AlertStatus.Resolved,
            _ => throw new ArgumentException(
                $"Status inválido: '{status}'. Use 'open' ou 'resolved'.",
                nameof(status))
        };
    }
}
