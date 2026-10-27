using InfraControl.Application.Reports.Crossings.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.Crossings.Queries;

/// <summary>
/// F25-T002 — indicadores integrados/cruzamentos entre módulos (docs/33 §5):
/// Chamados→Tasks, Tasks→Knowledge, PDI→Knowledge, Knowledge→Chamados e
/// Task→PDI (competências exigidas). <c>Administer</c> por agregar todos.
/// </summary>
public sealed record GetReportCrossingsQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : BaseQuery<ReportCrossingsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetReportCrossingsQueryHandler(
    IReportCrossingsRepository repository)
    : IRequestHandler<GetReportCrossingsQuery, ReportCrossingsDto>
{
    public Task<ReportCrossingsDto> Handle(
        GetReportCrossingsQuery request,
        CancellationToken cancellationToken)
        => repository.GetCrossingsAsync(
            request.From, request.To, cancellationToken);
}
