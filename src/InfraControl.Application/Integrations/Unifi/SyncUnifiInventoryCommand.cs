using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Unifi;

/// <summary>
/// Resultado do sync (F20-T001/ADR-028 §4): contagens por iteração —
/// devices criados/atualizados, referências novas e itens com erro
/// (device invidual com falha não derruba o restante do sync).
/// </summary>
public sealed record UnifiSyncResultDto(int Created, int Updated, int References, int Errors);

/// <summary>
/// Sincronização de inventário UniFi → cadastro local. Idempotente por MAC:
/// upsert de Device + ExternalDeviceReference (Source=UniFi, ExternalId =
/// MAC lowercase SEM separadores — decisão ADR-028 §7), location "UniFi"
/// auto-criada e gateway → DeviceType.Firewall. REGRA 8: só cadastro é
/// persistido (Device + ExternalDeviceReference) — telemetria de massa
/// (PoE/tráfego/clientes) NÃO entra aqui (F20-T003 consulta sob demanda).
/// Operação de escrita: nível Operate (roles pelo command, nunca literal no
/// controller) + entrada de auditoria.
/// </summary>
public sealed record SyncUnifiInventoryCommand
    : BaseCommand<UnifiSyncResultDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "UniFiSync";
}

public sealed class SyncUnifiInventoryCommandHandler(
    IUnifiApiClient unifiApiClient,
    IDeviceRepository deviceRepository,
    IExternalDeviceReferenceRepository referenceRepository,
    ILocationRepository locationRepository,
    ILogger<SyncUnifiInventoryCommandHandler> logger)
    : IRequestHandler<SyncUnifiInventoryCommand, UnifiSyncResultDto>
{
    /// <summary>Location usada pelo sync — criada automaticamente se não existir (decisão do usuário 03/10/2026).</summary>
    public const string UniFiLocationName = "UniFi";

    public async Task<UnifiSyncResultDto> Handle(
        SyncUnifiInventoryCommand request, CancellationToken cancellationToken)
    {
        // erros de configuração/credencial/transporte propagam (endpoint 502/503 na Parte 3)
        var controllerDevices = await unifiApiClient.ListDevicesAsync(cancellationToken);

        if (controllerDevices.Count == 0)
            return new UnifiSyncResultDto(0, 0, 0, 0);

        var location = await GetOrCreateLocationAsync(cancellationToken);

        var created = 0;
        var updated = 0;
        var references = 0;
        var errors = 0;

        foreach (var dto in controllerDevices)
        {
            try
            {
                // decisão ADR-028 §7: ExternalId = MAC lowercase SEM separadores
                var externalId = CompactMac(dto.Mac);
                if (!IsValidMac(externalId))
                    throw new ArgumentException(
                        $"MAC inválido no cadastro do UniFi: '{dto.Mac}'.");

                var (device, wasCreated) = await UpsertDeviceAsync(location, dto, cancellationToken);
                if (wasCreated)
                    created++;
                else
                    updated++;

                if (await UpsertReferenceAsync(device, externalId, cancellationToken))
                    references++;
            }
            catch (Exception exception)
            {
                errors++;
                logger.LogWarning(exception,
                    "Sync UniFi: falha ao processar o device {Mac} ({Name})",
                    dto.Mac, dto.Name);
            }
        }

        return new UnifiSyncResultDto(created, updated, references, errors);
    }

    private async Task<(Device Device, bool Created)> UpsertDeviceAsync(
        Location location, UnifiDeviceDto dto, CancellationToken cancellationToken)
    {
        var existing = await deviceRepository.GetByMacAsync(dto.Mac, cancellationToken);

        if (existing is not null)
        {
            existing.Update(
                DisplayName(dto),
                MapType(dto.Type),
                existing.RackId,
                dto.Ip,
                existing.Mac,
                existing.Serial,
                dto.Model,
                existing.Vendor,
                existing.VlanId,
                existing.Description);
            await deviceRepository.SaveChangesAsync(cancellationToken);
            return (existing, false);
        }

        var device = Device.Create(
            location.Id,
            DisplayName(dto),
            MapType(dto.Type),
            ip: dto.Ip,
            mac: dto.Mac,
            model: dto.Model);

        await deviceRepository.AddAsync(device, cancellationToken);
        await deviceRepository.SaveChangesAsync(cancellationToken);
        return (device, true);
    }

    /// <summary>True = referência nova criada (referência existente do mesmo dono não conta).</summary>
    private async Task<bool> UpsertReferenceAsync(
        Device device, string externalId, CancellationToken cancellationToken)
    {
        var owner = await referenceRepository.GetDeviceIdAsync(
            ExternalDeviceSource.UniFi, externalId, cancellationToken);

        if (owner is null)
        {
            await referenceRepository.AddAsync(
                ExternalDeviceReference.Create(ExternalDeviceSource.UniFi, externalId, device.Id),
                cancellationToken);
            await referenceRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (owner != device.Id)
            throw new InvalidOperationException(
                $"A referência UniFi '{externalId}' já aponta para outro device.");

        return false;
    }

    private async Task<Location> GetOrCreateLocationAsync(CancellationToken cancellationToken)
    {
        var location = await locationRepository.GetByNameAsync(
            UniFiLocationName, cancellationToken);
        if (location is not null)
            return location;

        location = Location.Create(
            UniFiLocationName,
            "Local criado automaticamente pelo sync do UniFi Controller (F20-T001).");
        await locationRepository.AddAsync(location, cancellationToken);
        await locationRepository.SaveChangesAsync(cancellationToken);
        return location;
    }

    /// <summary>Gateway UniFi (UDM/USG/UXG) → Firewall — decisão do usuário 03/10/2026.</summary>
    internal static DeviceType MapType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "udm" or "ugw" or "usg" or "uxg" or "usgw" => DeviceType.Firewall,
        "usw" => DeviceType.Switch,
        "uap" => DeviceType.AccessPoint,
        _ => DeviceType.Other
    };

    private static string DisplayName(UnifiDeviceDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Name))
            return dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Model))
            return dto.Model.Trim();
        return dto.Mac.Trim();
    }

    private static string CompactMac(string? mac)
        => (mac ?? string.Empty)
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .Trim()
            .ToLowerInvariant();

    private static bool IsValidMac(string compactMac)
        => compactMac.Length == 12 && compactMac.All(Uri.IsHexDigit);
}
