using InfraControl.Application.Reports.History.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.History.Queries;

/// <summary>
/// F25-T005 §13 — série histórica mensal dos indicadores (Operate).
/// </summary>
public sealed record GetReportHistoryQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : BaseQuery<ReportHistoryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetReportHistoryQueryHandler(
    IReportHistoryRepository repository)
    : IRequestHandler<GetReportHistoryQuery, ReportHistoryDto>
{
    public Task<ReportHistoryDto> Handle(
        GetReportHistoryQuery request,
        CancellationToken cancellationToken)
        => repository.GetHistoryAsync(request.From, request.To, cancellationToken);
}
