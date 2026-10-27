using InfraControl.Application.Reports.Dashboards.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Reports.Dashboards.Queries;

// ── §6 Colaborador ────────────────────────────────────────────────────────

/// <summary>
/// F25-T003 §6 — dashboard do colaborador (minhas Tasks, meu PDI, meu
/// conhecimento). Escopo do usuário autenticado; policy <c>Operate</c>.
/// </summary>
public sealed record GetColaboradorDashboardQuery
    : BaseQuery<ColaboradorDashboardDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetColaboradorDashboardQueryHandler(
    IReportDashboardsRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetColaboradorDashboardQuery, ColaboradorDashboardDto>
{
    public Task<ColaboradorDashboardDto> Handle(
        GetColaboradorDashboardQuery request,
        CancellationToken cancellationToken)
        => repository.GetColaboradorAsync(
            currentUser.UserId ?? Guid.Empty, cancellationToken);
}

// ── §7 Gestor ─────────────────────────────────────────────────────────────

/// <summary>
/// F25-T003 §7 — dashboard do gestor: visão operacional ampla + listas de
/// atenção. Policy <c>Operate</c>.
/// </summary>
public sealed record GetGestorDashboardQuery
    : BaseQuery<GestorDashboardDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetGestorDashboardQueryHandler(
    IReportDashboardsRepository repository)
    : IRequestHandler<GetGestorDashboardQuery, GestorDashboardDto>
{
    public Task<GestorDashboardDto> Handle(
        GetGestorDashboardQuery request,
        CancellationToken cancellationToken)
        => repository.GetGestorAsync(cancellationToken);
}

// ── §8 Administrativo ─────────────────────────────────────────────────────

/// <summary>
/// F25-T003 §8 — dashboard administrativo consolidado com filtros (período,
/// responsável, tipo). Policy <c>Administer</c>.
/// </summary>
public sealed record GetAdministrativoDashboardQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? ResponsibleId = null,
    TaskKindFilter? Kind = null)
    : BaseQuery<AdministrativoDashboardDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class GetAdministrativoDashboardQueryHandler(
    IReportDashboardsRepository repository)
    : IRequestHandler<GetAdministrativoDashboardQuery, AdministrativoDashboardDto>
{
    public Task<AdministrativoDashboardDto> Handle(
        GetAdministrativoDashboardQuery request,
        CancellationToken cancellationToken)
        => repository.GetAdministrativoAsync(
            request.From, request.To, request.ResponsibleId, request.Kind,
            cancellationToken);
}
