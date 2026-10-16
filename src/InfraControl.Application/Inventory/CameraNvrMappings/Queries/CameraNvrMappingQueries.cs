using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.CameraNvrMappings.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.CameraNvrMappings.Queries;

// Sem termo livre (filtros por id, F30-T005c). Cache 5min via BaseQuery —
// mesmo molde de SearchProjectsQuery (ADR-043); commands não declararam
// invalidação nos módulos irmãos (registro do checkpoint T005c).

public sealed record SearchCameraNvrMappingsQuery(
    Guid? NvrId = null,
    Guid? CameraId = null,
    int Page = 1,
    int PageSize = 20)
    : BaseQuery<PagedResult<CameraNvrMappingDto>>, IPagedRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchCameraNvrMappingsQueryHandler(
    ICameraNvrMappingRepository repository)
    : IRequestHandler<SearchCameraNvrMappingsQuery, PagedResult<CameraNvrMappingDto>>
{
    public async Task<PagedResult<CameraNvrMappingDto>> Handle(
        SearchCameraNvrMappingsQuery request, CancellationToken cancellationToken)
    {
        var paging = Paging.Normalize(request);

        var result = await repository.SearchAsync(
            request.NvrId,
            request.CameraId,
            paging.Page,
            paging.PageSize,
            cancellationToken);

        return new PagedResult<CameraNvrMappingDto>(
            result.Items
                .Select(mapping => new CameraNvrMappingDto(
                    mapping.Id, mapping.CameraId, mapping.NvrId, mapping.ChannelNumber))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetCameraNvrMappingQuery(Guid Id)
    : BaseQuery<CameraNvrMappingDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetCameraNvrMappingQueryHandler(
    ICameraNvrMappingRepository repository)
    : IRequestHandler<GetCameraNvrMappingQuery, CameraNvrMappingDto>
{
    public async Task<CameraNvrMappingDto> Handle(
        GetCameraNvrMappingQuery request, CancellationToken cancellationToken)
    {
        var mapping = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Mapeamento '{request.Id}' não encontrado.");

        return new CameraNvrMappingDto(
            mapping.Id, mapping.CameraId, mapping.NvrId, mapping.ChannelNumber);
    }
}
