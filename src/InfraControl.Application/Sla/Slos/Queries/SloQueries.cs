using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Sla.Slos.Bindings.Ports;
using InfraControl.Application.Sla.Slos.Budgets.Ports;
using InfraControl.Application.Sla.Slos.Measurements.Ports;
using InfraControl.Application.Sla.Slos.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Slos.Queries;

// Sem cache (IQuery direto): SLO muda a cada mutation.

public sealed record SearchSlosQuery(
    string? Term = null,
    Guid? ServiceId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SloDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchSlosQueryHandler(
    ISloRepository repository,
    ISloMeasurementRepository measurements,
    ISloBudgetRepository budgets,
    ISloCheckBindingRepository checkBindings)
    : IRequestHandler<SearchSlosQuery, PagedResult<SloDto>>
{
    public async Task<PagedResult<SloDto>> Handle(
        SearchSlosQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            term, request.ServiceId, page, pageSize, cancellationToken);

        var enriched = await EnrichAsync(
            result.Items, measurements, budgets, checkBindings, cancellationToken);

        return new PagedResult<SloDto>(
            enriched,
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    /// <summary>Status real por SLO: última medição na janela + orçamento vigente.</summary>
    internal static async Task<SloDto[]> EnrichAsync(
        IReadOnlyCollection<Slo> slos,
        ISloMeasurementRepository measurements,
        ISloBudgetRepository budgets,
        ISloCheckBindingRepository checkBindings,
        CancellationToken cancellationToken)
    {
        if (slos.Count == 0)
            return [];

        var now = DateTime.UtcNow;
        // Janela mais larga da página — cada SLO filtra pela sua depois.
        var maxWindow = slos.Max(slo => slo.WindowDays);
        var sinceUtc = now.AddDays(-maxWindow);
        var ids = slos.Select(slo => slo.Id).ToArray();

        var latestMeasurements = await measurements.GetLatestBySloIdsAsync(
            ids, sinceUtc, cancellationToken);
        var latestBudgets = await budgets.GetLatestBySloIdsAsync(
            ids, cancellationToken);
        var boundChecks = await checkBindings.GetCheckIdsBySloIdsAsync(
            ids, cancellationToken);

        return slos.Select(slo =>
        {
            var measurement = latestMeasurements.TryGetValue(slo.Id, out var m)
                && m.MeasuredAtUtc >= now.AddDays(-slo.WindowDays)
                    ? m : null;
            var budget = latestBudgets.TryGetValue(slo.Id, out var b) ? b : null;
            return new SloDto(
                slo.Id, slo.Name, slo.TargetPercent, slo.WindowDays, slo.ServiceId,
                measurement?.ValuePercent,
                measurement?.MeasuredAtUtc,
                budget?.ConsumedPercent,
                budget?.PeriodEndUtc,
                boundChecks.TryGetValue(slo.Id, out var checks) ? checks : []);
        }).ToArray();
    }
}

public sealed record GetSloQuery(Guid Id) : IQuery<SloDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetSloQueryHandler(
    ISloRepository repository,
    ISloMeasurementRepository measurements,
    ISloBudgetRepository budgets,
    ISloCheckBindingRepository checkBindings)
    : IRequestHandler<GetSloQuery, SloDto>
{
    public async Task<SloDto> Handle(GetSloQuery request, CancellationToken cancellationToken)
    {
        var slo = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"SLO '{request.Id}' não encontrado.");

        var enriched = await SearchSlosQueryHandler.EnrichAsync(
            [slo], measurements, budgets, checkBindings, cancellationToken);
        return enriched[0];
    }
}
