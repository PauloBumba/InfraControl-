using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// F26 Fase 7: correlação UniFi↔inventário sobre `ExternalDeviceReference`
/// (ADR-028 §7 — ExternalId = MAC compacto lowercase). Auto-match já é
/// feito pelo sync; aqui fica a VISÃO (matched/unlinked/stale/conflitos) e
/// as ações manuais de vínculo. Remover vínculo apaga SÓ a referência —
/// o histórico do Device nunca é destruído (7.3).
/// </summary>
public sealed record UniFiAssetLinkDto(
    string Mac,
    Guid DeviceId,
    string? DeviceName,
    string Source,
    bool ControllerPresent);

public sealed record UniFiUnlinkedDeviceDto(
    string Mac, string? Name, string? Model, string? Type, string? State);

public sealed record UniFiCorrelationDto(
    IReadOnlyList<UniFiAssetLinkDto> Matched,
    IReadOnlyList<UniFiUnlinkedDeviceDto> Unlinked,
    IReadOnlyList<UniFiAssetLinkDto> Conflicts);

public sealed record GetUniFiCorrelationQuery : IQuery<UniFiCorrelationDto>
{
}

public sealed class GetUniFiCorrelationQueryHandler(
    IExternalDeviceReferenceRepository referenceRepository,
    IDeviceRepository deviceRepository,
    IUnifiApiClient unifiApiClient)
    : IRequestHandler<GetUniFiCorrelationQuery, UniFiCorrelationDto>
{
    public async Task<UniFiCorrelationDto> Handle(
        GetUniFiCorrelationQuery request, CancellationToken cancellationToken)
    {
        var refs = await referenceRepository.ListBySourceAsync(
            ExternalDeviceSource.UniFi, cancellationToken);
        var controllerDevices = await unifiApiClient.ListDevicesAsync(cancellationToken);
        var controllerMacs = controllerDevices
            .ToDictionary(d => CompactMac(d.Mac), d => d, StringComparer.OrdinalIgnoreCase);

        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);
        var byId = devices.ToDictionary(d => d.Id);

        var matched = new List<UniFiAssetLinkDto>();
        var conflicts = new List<UniFiAssetLinkDto>();
        var linkedMacs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in refs)
        {
            linkedMacs.Add(reference.ExternalId);
            byId.TryGetValue(reference.DeviceId, out var device);
            var deviceMac = device?.Mac is null ? null : CompactMac(device.Mac);
            var link = new UniFiAssetLinkDto(
                reference.ExternalId,
                reference.DeviceId,
                device?.Name,
                nameof(ExternalDeviceSource.UniFi),
                ControllerPresent: controllerMacs.ContainsKey(reference.ExternalId));

            // conflito: Device dono tem MAC diferente do vínculo
            if (device is not null && deviceMac is not null &&
                !deviceMac.Equals(reference.ExternalId, StringComparison.OrdinalIgnoreCase))
                conflicts.Add(link);
            else
                matched.Add(link);
        }

        var unlinked = controllerMacs
            .Where(pair => !linkedMacs.Contains(pair.Key))
            .Select(pair => new UniFiUnlinkedDeviceDto(
                pair.Key,
                pair.Value.Name,
                pair.Value.Model,
                pair.Value.Type,
                pair.Value.State))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new UniFiCorrelationDto(matched, unlinked, conflicts);
    }

    private static string CompactMac(string mac)
        => mac.Replace(":", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
}

public sealed record CreateUniFiAssetLinkCommand(string Mac, Guid DeviceId)
    : ICommand<UniFiAssetLinkDto>
{
}

public sealed class CreateUniFiAssetLinkCommandHandler(
    IExternalDeviceReferenceRepository referenceRepository,
    IDeviceRepository deviceRepository)
    : IRequestHandler<CreateUniFiAssetLinkCommand, UniFiAssetLinkDto>
{
    public async Task<UniFiAssetLinkDto> Handle(
        CreateUniFiAssetLinkCommand request, CancellationToken cancellationToken)
    {
        var mac = CompactMac(request.Mac);
        if (mac.Length != 12 || !mac.All(Uri.IsHexDigit))
            throw new ArgumentException("MAC inválido.", nameof(request));

        var existingOwner = await referenceRepository.GetDeviceIdAsync(
            ExternalDeviceSource.UniFi, mac, cancellationToken);
        if (existingOwner is not null)
            throw new InvalidOperationException(
                $"O MAC '{mac}' já está vinculado a outro device.");

        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException("Device não encontrado no inventário.");

        var deviceMac = device.Mac is null ? null : CompactMac(device.Mac);
        if (deviceMac is not null && !deviceMac.Equals(mac, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"O device '{device.Name}' possui MAC '{device.Mac}' — corrija o cadastro antes de vincular.");

        var ownerOfMac = await deviceRepository.GetByMacAsync(request.Mac, cancellationToken);
        if (ownerOfMac is not null && ownerOfMac.Id != device.Id)
            throw new InvalidOperationException(
                $"O MAC '{mac}' já pertence ao device '{ownerOfMac.Name}'.");

        var reference = ExternalDeviceReference.Create(
            ExternalDeviceSource.UniFi, mac, device.Id);
        await referenceRepository.AddAsync(reference, cancellationToken);
        await referenceRepository.SaveChangesAsync(cancellationToken);

        return new UniFiAssetLinkDto(
            mac, device.Id, device.Name, nameof(ExternalDeviceSource.UniFi),
            ControllerPresent: false);
    }

    private static string CompactMac(string mac)
        => mac.Replace(":", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
}

public sealed record DeleteUniFiAssetLinkCommand(string Mac) : ICommand
{
}

public sealed class DeleteUniFiAssetLinkCommandHandler(
    IExternalDeviceReferenceRepository referenceRepository)
    : IRequestHandler<DeleteUniFiAssetLinkCommand>
{
    public async Task Handle(
        DeleteUniFiAssetLinkCommand request, CancellationToken cancellationToken)
    {
        var reference = await referenceRepository.GetBySourceAndExternalIdAsync(
            ExternalDeviceSource.UniFi, request.Mac, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Não existe vínculo UniFi para o MAC '{request.Mac}'.");

        // só o vínculo — o Device e seu histórico permanecem (Fase 7.3)
        referenceRepository.Remove(reference);
        await referenceRepository.SaveChangesAsync(cancellationToken);
    }
}
