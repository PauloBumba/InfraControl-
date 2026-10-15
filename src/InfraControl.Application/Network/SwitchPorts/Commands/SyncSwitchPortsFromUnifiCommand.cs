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
// do controller UniFi. Portas existentes (por nome) têm a OBSERVAÇÃO
// atualizada (LinkUp/velocidade/duplex — F30-T001 S4a); portas novas são
// criadas; portas manuais ausentes do controller são PRESERVADAS (não
// deletadas — podem ser de switch non-UniFi ou registro manual legítimo).
// REGRA 8: contadores/PoE NÃO são persistidos (ADR-028 §10 — PoE continua
// ao vivo); velocidade/duplex passaram a persistir por serem cadastro
// semi-estático (docs/42 §B, §7 itens 8-9 do docs/51).

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
                // Re-fetch tracked — SearchAsync é AsNoTracking. Observe grava
                // link/velocidade/duplex (cadastro manual intacto) e devolve
                // false quando nada mudou — sync idempotente não conta update.
                var tracked = await repository.GetByIdAsync(
                    existingPort.Id, cancellationToken);
                if (tracked is not null
                    && tracked.Observe(
                        controllerPort.LinkUp,
                        SwitchPortSpeedParser.Parse(controllerPort.Speed),
                        SwitchPortDuplexParser.Parse(controllerPort.FullDuplex)))
                {
                    updated++;
                }
            }
            else
            {
                var newPort = SwitchPort.Create(
                    request.SwitchId, name, description: null);
                newPort.Observe(
                    controllerPort.LinkUp,
                    SwitchPortSpeedParser.Parse(controllerPort.Speed),
                    SwitchPortDuplexParser.Parse(controllerPort.FullDuplex));
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
