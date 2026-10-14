using InfraControl.Application.Reports.Transversal.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.Transversal.Queries;

/// <summary>
/// Relatório transversal (ADR-049) — agregado de cima dos 5 módulos.
/// <c>BaseQuery</c> (cacheável via CachingBehavior/CacheKeyBuilder, 5 min) e
/// <c>Operate</c> (ADR-030): o snapshot é agregado global de operações — o
/// Operator do Centro de Operações vê as contagens; não expõe registro
/// pessoal por usuário (isso fica nos dashboards por vertical).
/// </summary>
public sealed record GetTransversalReportQuery(
    DateOnly? From = null,
    DateOnly? To = null)
    : BaseQuery<TransversalReportDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetTransversalReportQueryHandler(IReportRepository reports)
    : IRequestHandler<GetTransversalReportQuery, TransversalReportDto>
{
    public async Task<TransversalReportDto> Handle(
        GetTransversalReportQuery request,
        CancellationToken cancellationToken)
        => await reports.GetSnapshotAsync(
            request.From, request.To, cancellationToken);
}
