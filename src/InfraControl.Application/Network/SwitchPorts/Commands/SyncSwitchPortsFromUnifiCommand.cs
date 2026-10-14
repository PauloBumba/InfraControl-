using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.SwitchPorts.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Network.SwitchPorts.Commands;

// Fase 07c — sincroniza o cadastro de SwitchPort a partir do port_table
// do controller UniFi. Portas existentes (por nome) têm LinkUp atualizado;
// portas novas são criadas; portas manuais ausentes do controller são
// PRESERVADAS (não deletadas — podem ser de switch non-UniFi ou registro
// manual legítimo). REGRA 8: contadores/PoE NÃO são persistidos.

public sealed record SyncPortsResultDto(
    int Created,
    int Updated,
    int TotalFromController,
    int TotalAfterSync);

public sealed record SyncSwitchPortsFromUnifiCommand(Guid SwitchId)
    : BaseCommand<SyncPortsResultDto>, IAuthorizedRequest
{
    public override IReadOnlyCollection<string> CacheKeysToInvalidate =>
        [$"switchPorts:{SwitchId}"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SyncSwitchPortsFromUnifiCommandHandler(
    ISwitchPortRepository repository,
    GetSwitchQueryHandler switchQueryHandler,
    IExternalDeviceReferenceRepository referenceRepository,
    IUnifiApiClient unifiApiClient,
    ILogger<SyncSwitchPortsFromUnifiCommandHandler> logger)
    : IRequestHandler<SyncSwitchPortsFromUnifiCommand, SyncPortsResultDto>
{
    // Teto de portas por página (clamp 1..100 do SearchAsync).
    private const int PageSize = 100;

    public async Task<SyncPortsResultDto> Handle(
        SyncSwitchPortsFromUnifiCommand request, CancellationToken cancellationToken)
    {
        // Guard de type=Switch (404 pt-BR).
        await switchQueryHandler.Handle(
            new GetSwitchQuery(request.SwitchId), cancellationToken);

        var references = await referenceRepository.ListByDeviceIdAsync(
            request.SwitchId, cancellationToken);
        var unifiRef = references.FirstOrDefault(r => r.Source == ExternalDeviceSource.UniFi)
            ?? throw new InvalidOperationException(
                "O switch não tem referência UniFi cadastrada — sincronização indisponível.");

        IReadOnlyList<UnifiSwitchPortDto> controllerPorts;
        try
        {
            controllerPorts = await unifiApiClient.ListSwitchPortsAsync(
                unifiRef.ExternalId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            throw new InvalidOperationException(
                "O device não foi encontrado no controller UniFi (removido ou não adotado).");
        }

        // Carga existente — SearchAsync é AsNoTracking; para atualizar,
        // re-fetch com GetByIdAsync (tracked) e Update.
        var existing = new List<SwitchPort>();
        var page = 1;
        while (true)
        {
            var batch = await repository.SearchAsync(
                request.SwitchId, null, page, PageSize, cancellationToken);
            existing.AddRange(batch.Items);
            if (batch.Items.Count < PageSize) break;
            page++;
        }

        var byName = existing.ToDictionary(
            p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;

        foreach (var controllerPort in controllerPorts)
        {
            // Usa o nome do controller; fallback "Port {idx}" se vazio.
            var name = string.IsNullOrWhiteSpace(controllerPort.Name)
                ? $"Port {controllerPort.PortIndex}"
                : controllerPort.Name.Trim();

            if (byName.TryGetValue(name, out var existingPort))
            {
                // Atualiza apenas LinkUp (name/description já existem).
                // Re-fetch tracked — SearchAsync é AsNoTracking.
                var tracked = await repository.GetByIdAsync(
                    existingPort.Id, cancellationToken);
                if (tracked is not null && tracked.LinkUp != controllerPort.LinkUp)
                {
                    tracked.Update(tracked.Name, tracked.Description, controllerPort.LinkUp);
                    updated++;
                }
            }
            else
            {
                var newPort = SwitchPort.Create(
                    request.SwitchId, name, description: null);
                newPort.Update(newPort.Name, newPort.Description, controllerPort.LinkUp);
                repository.AddAsync(newPort, cancellationToken);
                byName[name] = newPort;
                created++;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "UniFi port sync for switch {DeviceId}: {Created} created, {Updated} updated, {FromController} from controller.",
            request.SwitchId, created, updated, controllerPorts.Count);

        return new SyncPortsResultDto(
            created, updated, controllerPorts.Count, byName.Count);
    }
}
