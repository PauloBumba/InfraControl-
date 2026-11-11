using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Projects.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Checklists;

/// <summary>
/// Validación de existência do dono polimórfico do checklist
/// (F50-T007/ADR-067) — switch por tipo no handler, precedente
/// `KnowledgeArticleOriginCommands.EnsureOriginExistsAsync`: 404 pt-BR
/// no formato "{Noun} '{id}' não encontrado.".
/// </summary>
public static class ChecklistOwners
{
    public static async Task EnsureExistsAsync(
        ChecklistOwnerType ownerType,
        Guid ownerId,
        IDeviceRepository deviceRepository,
        ILocationRepository locationRepository,
        IIncidentRepository incidentRepository,
        IProjectRepository projectRepository,
        CancellationToken cancellationToken = default)
    {
        var exists = ownerType switch
        {
            ChecklistOwnerType.Device =>
                await deviceRepository.GetByIdAsync(ownerId, cancellationToken)
                    is not null,
            ChecklistOwnerType.Location =>
                await locationRepository.GetByIdAsync(ownerId, cancellationToken)
                    is not null,
            ChecklistOwnerType.Incident =>
                await incidentRepository.GetByIdAsync(ownerId, cancellationToken)
                    is not null,
            ChecklistOwnerType.Project =>
                await projectRepository.GetByIdAsync(ownerId, cancellationToken)
                    is not null,
            _ => false,
        };

        if (!exists)
            throw new KeyNotFoundException(ownerType switch
            {
                ChecklistOwnerType.Device =>
                    $"Device '{ownerId}' não encontrado.",
                ChecklistOwnerType.Location =>
                    $"Local '{ownerId}' não encontrado.",
                ChecklistOwnerType.Incident =>
                    $"Incidente '{ownerId}' não encontrado.",
                _ => $"Projeto '{ownerId}' não encontrado.",
            });
    }
}
