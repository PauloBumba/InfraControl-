using InfraControl.Application.Checklists.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Checklists.Queries;

// F50-T007/ADR-067 — listagem dos itens do checklist genérico (ordem
// de criação, mesmo guard de 404 do dono da criação).

public sealed record GetChecklistItemsQuery(string OwnerType, Guid OwnerId)
    : IQuery<IReadOnlyCollection<ChecklistItemDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetChecklistItemsQueryHandler(
    IDeviceRepository deviceRepository,
    ILocationRepository locationRepository,
    IIncidentRepository incidentRepository,
    IProjectRepository projectRepository,
    IChecklistItemRepository itemRepository)
    : IRequestHandler<GetChecklistItemsQuery, IReadOnlyCollection<ChecklistItemDto>>
{
    public async Task<IReadOnlyCollection<ChecklistItemDto>> Handle(
        GetChecklistItemsQuery request, CancellationToken cancellationToken)
    {
        var ownerType = ChecklistOwnerTypes.Parse(request.OwnerType);

        await ChecklistOwners.EnsureExistsAsync(
            ownerType, request.OwnerId,
            deviceRepository, locationRepository,
            incidentRepository, projectRepository,
            cancellationToken);

        var rows = await itemRepository.GetByOwnerAsync(
            ownerType, request.OwnerId, cancellationToken);

        return rows
            .Select(item => new ChecklistItemDto(
                item.Id,
                item.OwnerType.ToString(),
                item.OwnerId,
                item.Description,
                item.IsDone,
                item.DoneAt,
                item.CreateDateTime))
            .ToArray();
    }
}
