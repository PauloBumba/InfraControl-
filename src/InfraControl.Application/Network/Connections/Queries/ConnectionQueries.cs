using InfraControl.Application.Network.Connections.Ports;

namespace InfraControl.Application.Network.Connections.Queries;

// F3-T005: leitura de conexões — paged com pontas resolvidas (join no
// repositório). Guards 404 pt-BR.

public sealed record SearchConnectionsQuery(
    Guid? DeviceId = null,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ConnectionDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class SearchConnectionsQueryHandler(IConnectionRepository repository)
    : IRequestHandler<SearchConnectionsQuery, PagedResult<ConnectionDto>>
{
    public async Task<PagedResult<ConnectionDto>> Handle(
        SearchConnectionsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            request.DeviceId, term, page, pageSize, cancellationToken);

        return new PagedResult<ConnectionDto>(
            result.Items.Select(item => new ConnectionDto(
                item.Connection.Id,
                item.Source.Id, item.Source.Name, item.Source.DeviceId,
                item.Target.Id, item.Target.Name, item.Target.DeviceId)).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetConnectionQuery(Guid Id) : IQuery<ConnectionDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryRead.ToString()];
}

public sealed class GetConnectionQueryHandler(IConnectionRepository repository)
    : IRequestHandler<GetConnectionQuery, ConnectionDto>
{
    public async Task<ConnectionDto> Handle(
        GetConnectionQuery request,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetWithEndpointsAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Conexão '{request.Id}' não encontrada.");

        return new ConnectionDto(
            item.Connection.Id,
            item.Source.Id, item.Source.Name, item.Source.DeviceId,
            item.Target.Id, item.Target.Name, item.Target.DeviceId);
    }
}
