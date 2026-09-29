using InfraControl.Application.Discovery.Results;
using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Application.Discovery.Runs.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Discovery.Results.Queries;

// F4-T002: somente leitura — escrita vem com os providers (F4-T003+).
// Ordenação por CreatedAt DESC; lista de run inexistente → 404 (mesmo
// tratamento do GET de runs).

public sealed record SearchDiscoveryResultsQuery(
    Guid RunId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<DiscoveryResultDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchDiscoveryResultsQueryHandler(
    IDiscoveryResultRepository repository,
    IDiscoveryRunRepository runRepository)
    : IRequestHandler<SearchDiscoveryResultsQuery, PagedResult<DiscoveryResultDto>>
{
    public async Task<PagedResult<DiscoveryResultDto>> Handle(
        SearchDiscoveryResultsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        _ = await runRepository.GetByIdAsync(request.RunId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Execução de discovery '{request.RunId}' não encontrada.");

        var result = await repository.SearchByRunAsync(
            request.RunId, page, pageSize, cancellationToken);

        return new PagedResult<DiscoveryResultDto>(
            result.Items.Select(Map).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    internal static DiscoveryResultDto Map(Domain.Entities.DiscoveryResult result) => new(
        result.Id,
        result.DiscoveryRunId,
        result.Kind.ToString(),
        result.Summary,
        result.Values,
        result.DeviceId,
        result.CreatedAt);
}

public sealed record GetDiscoveryResultQuery(Guid Id) : IQuery<DiscoveryResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetDiscoveryResultQueryHandler(IDiscoveryResultRepository repository)
    : IRequestHandler<GetDiscoveryResultQuery, DiscoveryResultDto>
{
    public async Task<DiscoveryResultDto> Handle(
        GetDiscoveryResultQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Resultado de discovery '{request.Id}' não encontrado.");

        return SearchDiscoveryResultsQueryHandler.Map(result);
    }
}
