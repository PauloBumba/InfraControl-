using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Network.SwitchPorts.Ports;
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
/// Fase 07d: PortsCreated/PortsUpdated — portas de switches sincronizadas
/// automaticamente do port_table.
/// </summary>
public sealed record UnifiSyncResultDto(
    int Created, int Updated, int References, int Errors, int Sites = 0,
    int PortsCreated = 0, int PortsUpdated = 0);

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
    IUniFiSitesRepository sitesRepository,
    IUniFiSyncRunsRepository syncRunsRepository,
    ISwitchPortRepository switchPortRepository,
    ILogger<SyncUnifiInventoryCommandHandler> logger)
    : IRequestHandler<SyncUnifiInventoryCommand, UnifiSyncResultDto>
{
    /// <summary>Location usada pelo sync — criada automaticamente se não existir (decisão do usuário 03/10/2026).</summary>
    public const string UniFiLocationName = "UniFi";

    /// <summary>
    /// F26 Fase 9.1: Toda execução vira uma linha `UniFiSyncRun`
    /// (início/término/resultado/contagens; erro sanitizado). Falha
    /// propaga — o run falho é gravado best-effort antes do rethrow.
    /// </summary>
    public async Task<UnifiSyncResultDto> Handle(
        SyncUnifiInventoryCommand request, CancellationToken cancellationToken)
    {
        var run = UniFiSyncRun.Start(DateTime.UtcNow);
        try
        {
            var result = await HandleCore(request, cancellationToken);
            run.CompleteSuccess(DateTime.UtcNow, result.Created, result.Updated,
                result.References, result.Errors, result.Sites);
            await syncRunsRepository.AddAsync(run, cancellationToken);
            await syncRunsRepository.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            run.CompleteFailure(DateTime.UtcNow, exception.Message);
            try
            {
                await syncRunsRepository.AddAsync(run, CancellationToken.None);
                await syncRunsRepository.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception historyException)
            {
                logger.LogWarning(historyException,
                    "Sync UniFi: falha ao gravar o histórico da execução");
            }

            throw;
        }
    }

    private async Task<UnifiSyncResultDto> HandleCore(
        SyncUnifiInventoryCommand request, CancellationToken cancellationToken)
    {
        // F26 Fase 3.1: sites primeiro (cadastro); falha de sites não
        // derruba o sync de devices (assíncrono aos erros por device).
        var sites = 0;
        try
        {
            sites = await SyncSitesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Sync UniFi: falha ao sincronizar sites");
        }

        // erros de configuração/credencial/transporte propagam (endpoint 502/503 na Parte 3)
        var controllerDevices = await unifiApiClient.ListDevicesAsync(cancellationToken);

        if (controllerDevices.Count == 0)
            return new UnifiSyncResultDto(0, 0, 0, 0, sites);

        var location = await GetOrCreateLocationAsync(cancellationToken);

        var created = 0;
        var updated = 0;
        var references = 0;
        var errors = 0;
        var portsCreated = 0;
        var portsUpdated = 0;

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

                // Fase 07d — sincroniza portas automaticamente para switches.
                if (MapType(dto.Type) == DeviceType.Switch)
                {
                    try
                    {
                        var (pc, pu) = await SyncPortsAsync(
                            device.Id, dto.Mac, cancellationToken);
                        portsCreated += pc;
                        portsUpdated += pu;
                    }
                    catch (Exception portException)
                    {
                        // falha de portas não derruba o sync do device
                        logger.LogWarning(portException,
                            "Sync UniFi: falha ao sincronizar portas do switch {Mac} ({Name})",
                            dto.Mac, dto.Name);
                    }
                }
            }
            catch (Exception exception)
            {
                errors++;
                logger.LogWarning(exception,
                    "Sync UniFi: falha ao processar o device {Mac} ({Name})",
                    dto.Mac, dto.Name);
            }
        }

        return new UnifiSyncResultDto(
            created, updated, references, errors, sites, portsCreated, portsUpdated);
    }

    /// <summary>
    /// F26 Fase 3.1: upsert por <c>short_id</c>; site que sumiu do
    /// controller ganha <c>MissingSinceUtc</c> (nunca é apagado).
    /// </summary>
    private async Task<int> SyncSitesAsync(CancellationToken cancellationToken)
    {
        var controllerSites = await unifiApiClient.ListSitesAsync(cancellationToken);
        var synced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dto in controllerSites)
        {
            if (string.IsNullOrWhiteSpace(dto.ShortId))
                continue;

            var site = await sitesRepository.GetByShortIdAsync(dto.ShortId, cancellationToken);
            if (site is null)
            {
                await sitesRepository.AddAsync(
                    UniFiSite.Create(dto.ShortId, dto.Name, dto.Description, dto.Hidden),
                    cancellationToken);
            }
            else
            {
                site.UpdateFrom(dto.Name, dto.Description, dto.Hidden);
            }

            synced.Add(dto.ShortId);
        }

        foreach (var local in await sitesRepository.ListAsync(cancellationToken))
        {
            if (!synced.Contains(local.ShortId))
                local.MarkMissing();
        }

        await sitesRepository.SaveChangesAsync(cancellationToken);
        return synced.Count;
    }

    private async Task<(Device Device, bool Created)> UpsertDeviceAsync(
        Location location, UnifiDeviceDto dto, CancellationToken cancellationToken)
    {
        var existing = await deviceRepository.GetByMacAsync(dto.Mac, cancellationToken);
        var name = await ResolveNameAsync(existing?.Id, dto, cancellationToken);

        if (existing is not null)
        {
            existing.Update(
                name,
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
            name,
            MapType(dto.Type),
            ip: dto.Ip,
            mac: dto.Mac,
            model: dto.Model);

        await deviceRepository.AddAsync(device, cancellationToken);
        await deviceRepository.SaveChangesAsync(cancellationToken);
        return (device, true);
    }

    /// <summary>
    /// ux_devices_name é UNIQUE: dois devices com o mesmo nome (padrão de
    /// fábrica no controller, ou colisão com inventário existente) não podem
    /// derrubar o sync — suffixa com o final do MAC de forma determinística.
    /// </summary>
    private async Task<string> ResolveNameAsync(
        Guid? selfId, UnifiDeviceDto dto, CancellationToken cancellationToken)
    {
        var name = DisplayName(dto);
        var clash = await deviceRepository.GetByNameAsync(name, cancellationToken);
        if (clash is null || clash.Id == selfId)
            return name;

        var compact = CompactMac(dto.Mac);
        var baseName = name.Length > 111 ? name[..111] : name;
        var suffixed = $"{baseName} ({compact[^6..]})";
        var clash2 = await deviceRepository.GetByNameAsync(suffixed, cancellationToken);
        return clash2 is null || clash2.Id == selfId ? suffixed : compact;
    }

    /// <summary>
    /// Fase 07d — sincroniza portas de um switch a partir do port_table.
    /// Portas novas são criadas; existentes têm LinkUp atualizado; manuais
    /// ausentes do controller são preservadas. Retorna (created, updated).
    /// </summary>
    private async Task<(int Created, int Updated)> SyncPortsAsync(
        Guid deviceId, string mac, CancellationToken cancellationToken)
    {
        IReadOnlyList<UnifiSwitchPortDto> controllerPorts;
        try
        {
            controllerPorts = await unifiApiClient.ListSwitchPortsAsync(mac, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            // Device fora do controller — não é erro fatal para o sync.
            return (0, 0);
        }

        if (controllerPorts.Count == 0)
            return (0, 0);

        // Carga existente (páginas de 100 — switch típico cabe em 1).
        var existing = new List<SwitchPort>();
        var page = 1;
        while (true)
        {
            var batch = await switchPortRepository.SearchAsync(
                deviceId, null, page, 100, cancellationToken);
            existing.AddRange(batch.Items);
            if (batch.Items.Count < 100) break;
            page++;
        }

        var byName = existing.ToDictionary(
            p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;

        foreach (var controllerPort in controllerPorts)
        {
            var name = string.IsNullOrWhiteSpace(controllerPort.Name)
                ? $"Port {controllerPort.PortIndex}"
                : controllerPort.Name.Trim();

            if (byName.TryGetValue(name, out var existingPort))
            {
                // Re-fetch tracked — SearchAsync é AsNoTracking.
                var tracked = await switchPortRepository.GetByIdAsync(
                    existingPort.Id, cancellationToken);
                if (tracked is not null && tracked.LinkUp != controllerPort.LinkUp)
                {
                    tracked.Update(tracked.Name, tracked.Description, controllerPort.LinkUp);
                    updated++;
                }
            }
            else
            {
                var newPort = SwitchPort.Create(deviceId, name, description: null);
                newPort.Update(newPort.Name, newPort.Description, controllerPort.LinkUp);
                switchPortRepository.AddAsync(newPort, cancellationToken);
                byName[name] = newPort;
                created++;
            }
        }

        await switchPortRepository.SaveChangesAsync(cancellationToken);
        return (created, updated);
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
