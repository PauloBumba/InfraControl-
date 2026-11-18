using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Discovery.Runs.Queries;

// F4-T001: somente leitura — não há POST nesta task (gatilho manual por
// device vem com os providers F4-T003/T004). Ordenação por StartedAt DESC.

public sealed record SearchDiscoveryRunsQuery(
    Guid? DeviceId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<DiscoveryRunDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchDiscoveryRunsQueryHandler(IDiscoveryRunRepository repository)
    : IRequestHandler<SearchDiscoveryRunsQuery, PagedResult<DiscoveryRunDto>>
{
    public async Task<PagedResult<DiscoveryRunDto>> Handle(
        SearchDiscoveryRunsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await repository.SearchAsync(
            request.DeviceId, page, pageSize, cancellationToken);

        return new PagedResult<DiscoveryRunDto>(
            result.Items.Select(run => new DiscoveryRunDto(
                run.Id,
                run.DeviceId,
                run.Status.ToString(),
                run.StartedAt,
                run.FinishedAt,
                run.ErrorMessage)).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetDiscoveryRunQuery(Guid Id) : IQuery<DiscoveryRunDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetDiscoveryRunQueryHandler(IDiscoveryRunRepository repository)
    : IRequestHandler<GetDiscoveryRunQuery, DiscoveryRunDto>
{
    public async Task<DiscoveryRunDto> Handle(
        GetDiscoveryRunQuery request,
        CancellationToken cancellationToken)
    {
        var run = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Execução de discovery '{request.Id}' não encontrada.");

        return new DiscoveryRunDto(
            run.Id,
            run.DeviceId,
            run.Status.ToString(),
            run.StartedAt,
            run.FinishedAt,
            run.ErrorMessage);
    }
}

/// <summary>
/// Network Explorer F4 — lista os providers de discovery registrados
/// (chave canônica + nome) para a UI saber o que pode ser disparado.
/// Somente leitura.
/// </summary>
public sealed record DiscoveryProviderDto(string Key, string Name);

public sealed record ListDiscoveryProvidersQuery()
    : IQuery<IReadOnlyList<DiscoveryProviderDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class ListDiscoveryProvidersQueryHandler(
    IProviderRegistry<IDiscoveryProvider> providers)
    : IRequestHandler<ListDiscoveryProvidersQuery, IReadOnlyList<DiscoveryProviderDto>>
{
    public Task<IReadOnlyList<DiscoveryProviderDto>> Handle(
        ListDiscoveryProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var all = providers.GetAll();
        var result = DiscoverySweepProviders.Keys
            .Where(key => all.Any(p => p.CanHandle(key)))
            .Select(key => new DiscoveryProviderDto(
                key,
                all.First(p => p.CanHandle(key)).Name))
            .ToArray();

        return Task.FromResult<IReadOnlyList<DiscoveryProviderDto>>(result);
    }
}
