using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Network.SwitchPorts.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation. O Search reusa
// o guard de switch do F3-T001 (listar portas de device que não é switch → 404).

public sealed record SearchSwitchPortsQuery(
    Guid DeviceId,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<SwitchPortDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchSwitchPortsQueryHandler(
    ISwitchPortRepository repository,
    GetSwitchQueryHandler switchQueryHandler)
    : IRequestHandler<SearchSwitchPortsQuery, PagedResult<SwitchPortDto>>
{
    public async Task<PagedResult<SwitchPortDto>> Handle(
        SearchSwitchPortsQuery request,
        CancellationToken cancellationToken)
    {
        await switchQueryHandler.Handle(new GetSwitchQuery(request.DeviceId), cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(request.DeviceId, term, page, pageSize, cancellationToken);

        return new PagedResult<SwitchPortDto>(
            result.Items
                .Select(port => new SwitchPortDto(
                    port.Id, port.DeviceId, port.Name, port.Description, port.LinkUp))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetSwitchPortQuery(Guid Id) : IQuery<SwitchPortDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetSwitchPortQueryHandler(ISwitchPortRepository repository)
    : IRequestHandler<GetSwitchPortQuery, SwitchPortDto>
{
    public async Task<SwitchPortDto> Handle(GetSwitchPortQuery request, CancellationToken cancellationToken)
    {
        var port = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Porta '{request.Id}' não encontrado.");

        return new SwitchPortDto(port.Id, port.DeviceId, port.Name, port.Description, port.LinkUp);
    }
}
