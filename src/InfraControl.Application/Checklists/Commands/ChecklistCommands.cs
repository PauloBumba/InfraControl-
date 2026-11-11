using InfraControl.Application.Checklists.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Checklists.Commands;

// F50-T007/ADR-067 — checklist genérico: CRUD espelhando o
// TaskCriterion (F23-T003) — 404 pt-BR para dono/item inexistente,
// 400 do domínio para descrição vazia/acima de 300, null = mantém,
// no-op não persiste. Sem timeline: não existe histórico genérico —
// o agregado dono cuida do próprio histórico (ADR-067 §6).

public sealed record AddChecklistItemCommand(
    string OwnerType,
    Guid OwnerId,
    string Description) : BaseCommand<ChecklistItemDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AddChecklistItemCommandHandler(
    IDeviceRepository deviceRepository,
    ILocationRepository locationRepository,
    IIncidentRepository incidentRepository,
    IProjectRepository projectRepository,
    IChecklistItemRepository itemRepository,
    ILogger<AddChecklistItemCommandHandler> logger)
    : IRequestHandler<AddChecklistItemCommand, ChecklistItemDto>
{
    public async Task<ChecklistItemDto> Handle(
        AddChecklistItemCommand request, CancellationToken cancellationToken)
    {
        var ownerType = ChecklistOwnerTypes.Parse(request.OwnerType);

        await ChecklistOwners.EnsureExistsAsync(
            ownerType, request.OwnerId,
            deviceRepository, locationRepository,
            incidentRepository, projectRepository,
            cancellationToken);

        var item = ChecklistItem.Create(
            ownerType, request.OwnerId, request.Description);

        await itemRepository.AddAsync(item, cancellationToken);
        await itemRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Checklist item {ItemId} created ({OwnerType} {OwnerId}).",
            item.Id, item.OwnerType, item.OwnerId);

        return ToDto(item);
    }

    internal static ChecklistItemDto ToDto(ChecklistItem item) =>
        new(
            item.Id,
            item.OwnerType.ToString(),
            item.OwnerId,
            item.Description,
            item.IsDone,
            item.DoneAt,
            item.CreateDateTime);
}

// ADR-067 §6 — PUT do item (Operate): descrição e/ou marcação em um
// mesmo comando; null = mantém. No-op (nada mudou) não salva.

public sealed record UpdateChecklistItemCommand(
    Guid Id,
    string? Description = null,
    bool? Done = null) : BaseCommand<ChecklistItemDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateChecklistItemCommandHandler(
    IChecklistItemRepository itemRepository,
    ILogger<UpdateChecklistItemCommandHandler> logger)
    : IRequestHandler<UpdateChecklistItemCommand, ChecklistItemDto>
{
    public async Task<ChecklistItemDto> Handle(
        UpdateChecklistItemCommand request, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Item '{request.Id}' não encontrado.");

        var beforeDescription = item.Description;
        var beforeDone = item.IsDone;

        if (request.Description is { } description)
            item.EditDescription(description); // domínio: 400 se vazio/>300
        if (request.Done is { } done)
            item.SetDone(done);

        var descriptionChanged = beforeDescription != item.Description;
        var doneChanged = beforeDone != item.IsDone;

        if (!descriptionChanged && !doneChanged)
            return AddChecklistItemCommandHandler.ToDto(item); // no-op

        await itemRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Checklist item {ItemId} updated (done: {Done}).",
            item.Id, item.IsDone);

        return AddChecklistItemCommandHandler.ToDto(item);
    }
}

public sealed record DeleteChecklistItemCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteChecklistItemCommandHandler(
    IChecklistItemRepository itemRepository,
    ILogger<DeleteChecklistItemCommandHandler> logger)
    : IRequestHandler<DeleteChecklistItemCommand>
{
    public async Task Handle(
        DeleteChecklistItemCommand request, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Item '{request.Id}' não encontrado.");

        item.Delete(); // ADR-030 §5 — ChecklistItemDeletedDomainEvent
        itemRepository.Remove(item);
        await itemRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Checklist item {ItemId} deleted ({OwnerType} {OwnerId}).",
            item.Id, item.OwnerType, item.OwnerId);
    }
}
