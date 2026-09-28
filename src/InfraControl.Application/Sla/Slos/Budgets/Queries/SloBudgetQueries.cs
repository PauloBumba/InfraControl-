using InfraControl.Application.Sla.Slos.Budgets.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Sla.Slos.Budgets.Queries;

// Sem cache (IQuery direto): orçamentos mudam a cada registro.

public sealed record SearchSloBudgetsQuery(
    Guid SloId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SloBudgetDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaRead.ToString()];
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

public sealed record GetSloBudgetQuery(Guid Id)
    : IQuery<SloBudgetDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaRead.ToString()];
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
