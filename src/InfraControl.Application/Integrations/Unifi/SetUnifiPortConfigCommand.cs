using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Unifi;

// Fase 07e (ADR-051): escrita de configuração de porta via UniFi.
// GET → modify → PUT (nunca POST cego). Proteção de porta de management
// no cliente. RoleAccess.Operate + auditoria obrigatória.

public sealed record SetUnifiPortResultDto(bool Success, string Message);

public sealed record SetUnifiPortConfigCommand(
    Guid SwitchId,
    int PortIndex,
    bool? Disabled = null,
    int? PortVlan = null)
    : BaseCommand<SetUnifiPortResultDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;

    public string AuditResource => "UnifiPortConfig";
}

public sealed class SetUnifiPortConfigCommandHandler(
    GetSwitchQueryHandler switchQueryHandler,
    IExternalDeviceReferenceRepository referenceRepository,
    IUnifiApiClient unifiApiClient,
    ILogger<SetUnifiPortConfigCommandHandler> logger)
    : IRequestHandler<SetUnifiPortConfigCommand, SetUnifiPortResultDto>
{
    public async Task<SetUnifiPortResultDto> Handle(
        SetUnifiPortConfigCommand request, CancellationToken cancellationToken)
    {
        // Guard de type=Switch (404 pt-BR).
        await switchQueryHandler.Handle(
            new GetSwitchQuery(request.SwitchId), cancellationToken);

        if (request.Disabled is null && request.PortVlan is null)
            throw new ArgumentException(
                "Informe ao menos um campo para alterar (Disabled ou PortVlan).");

        var references = await referenceRepository.ListByDeviceIdAsync(
            request.SwitchId, cancellationToken);
        var unifiRef = references.FirstOrDefault(r => r.Source == ExternalDeviceSource.UniFi)
            ?? throw new InvalidOperationException(
                "O switch não tem referência UniFi cadastrada — operação indisponível.");

        // A validação de porta de management é feita dentro do cliente
        // (ADR-051) antes do PUT — InvalidOperationException → 409.
        await unifiApiClient.SetPortConfigAsync(
            unifiRef.ExternalId,
            request.PortIndex,
            request.Disabled,
            request.PortVlan,
            cancellationToken);

        var parts = new List<string>();
        if (request.Disabled is not null)
            parts.Add(request.Disabled.Value ? "desabilitada" : "habilitada");
        if (request.PortVlan is not null)
            parts.Add($"VLAN {request.PortVlan}");

        var message = $"Porta {request.PortIndex} {string.Join(" e ", parts)} com sucesso.";
        logger.LogInformation(
            "UniFi port config: switch {DeviceId}, port {PortIndex}, disabled={Disabled}, vlan={Vlan}.",
            request.SwitchId, request.PortIndex, request.Disabled, request.PortVlan);

        return new SetUnifiPortResultDto(true, message);
    }
}
