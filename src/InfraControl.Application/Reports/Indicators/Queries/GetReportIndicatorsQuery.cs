using InfraControl.Application.Reports.Indicators.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.Indicators.Queries;

/// <summary>
/// F25-T001 — indicadores detalhados de Tasks/PDI/Knowledge (docs/33 §2–§4).
/// Visão administrativa consolidada; dashboards por vertical (§6–§8) são
/// outra task. <c>Administer</c> por expor métricas agregadas de todos.
/// </summary>
public sealed record GetReportIndicatorsQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : BaseQuery<ReportIndicatorsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetReportIndicatorsQueryHandler(
    IReportIndicatorsRepository repository)
    : IRequestHandler<GetReportIndicatorsQuery, ReportIndicatorsDto>
{
    public Task<ReportIndicatorsDto> Handle(
        GetReportIndicatorsQuery request,
        CancellationToken cancellationToken)
        => repository.GetIndicatorsAsync(
            request.From, request.To, cancellationToken);
}
