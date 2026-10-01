using InfraControl.Application.Sla.Slos.Budgets.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Slos.Budgets.Queries;

// Sem cache (IQuery direto): orçamentos mudam a cada registro.

public sealed record SearchSloBudgetsQuery(
    Guid SloId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SloBudgetDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchSloBudgetsQueryHandler(ISloBudgetRepository repository)
    : IRequestHandler<SearchSloBudgetsQuery, PagedResult<SloBudgetDto>>
{
    public async Task<PagedResult<SloBudgetDto>> Handle(
        SearchSloBudgetsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await repository.SearchBySloIdAsync(
            request.SloId, page, pageSize, cancellationToken);

        return new PagedResult<SloBudgetDto>(
            result.Items
                .Select(b => new SloBudgetDto(
                    b.Id, b.SloId, b.PeriodStart, b.PeriodEnd, b.ConsumedPercent))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

// F18-T003: agregado por serviço — a SlaPage faz 1 pedido por visão em
// vez de 1 por SLO (N+1 aceito no F17-T005). Ordem e clamp iguais à
// busca por SLO; `ServiceId` nulo = todos os serviços.

public sealed record SearchSloBudgetsByServiceQuery(
    Guid? ServiceId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SloBudgetDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchSloBudgetsByServiceQueryHandler(ISloBudgetRepository repository)
    : IRequestHandler<SearchSloBudgetsByServiceQuery, PagedResult<SloBudgetDto>>
{
    public async Task<PagedResult<SloBudgetDto>> Handle(
        SearchSloBudgetsByServiceQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await repository.SearchByServiceIdAsync(
            request.ServiceId, page, pageSize, cancellationToken);

        return new PagedResult<SloBudgetDto>(
            result.Items
                .Select(b => new SloBudgetDto(
                    b.Id, b.SloId, b.PeriodStart, b.PeriodEnd, b.ConsumedPercent))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetSloBudgetQuery(Guid Id)
    : IQuery<SloBudgetDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetSloBudgetQueryHandler(ISloBudgetRepository repository)
    : IRequestHandler<GetSloBudgetQuery, SloBudgetDto>
{
    public async Task<SloBudgetDto> Handle(
        GetSloBudgetQuery request, CancellationToken cancellationToken)
    {
        var budget = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Orçamento de SLO '{request.Id}' não encontrado.");

        return new SloBudgetDto(
            budget.Id, budget.SloId, budget.PeriodStart,
            budget.PeriodEnd, budget.ConsumedPercent);
    }
}
