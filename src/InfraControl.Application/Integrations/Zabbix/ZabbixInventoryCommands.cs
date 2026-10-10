using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Zabbix;

/// <summary>Resumo da sincronização de hosts (F27 Fase 3.1).</summary>
public sealed record ZabbixSyncResultDto(
    int Created,
    int Updated,
    int Missing,
    int Errors,
    string? Version);

/// <summary>
/// F27 Fase 3.1: sincroniza os hosts do Zabbix → read-model local
/// (idempotente por hostid; host removido no Zabbix vira
/// <c>MissingSinceUtc</c> — nunca apagado). Falha por host conta em
/// <c>Errors</c> e não derruba o sync; falha de conexão/credencial
/// propaga (o caller responde 502/503).
/// </summary>
public sealed record SyncZabbixInventoryCommand
    : BaseCommand<ZabbixSyncResultDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "ZabbixSync";
}

public sealed class SyncZabbixInventoryCommandHandler(
    IZabbixClient client,
    IZabbixHostsRepository hostsRepository,
    IZabbixSettingsRepository settingsRepository,
    ILogger<SyncZabbixInventoryCommandHandler> logger)
    : IRequestHandler<SyncZabbixInventoryCommand, ZabbixSyncResultDto>
{
    public async Task<ZabbixSyncResultDto> Handle(
        SyncZabbixInventoryCommand request, CancellationToken cancellationToken)
    {
        var version = await client.GetVersionAsync(cancellationToken);
        var hosts = await client.ListHostsAsync(cancellationToken);
        var groupNames = await LoadGroupNamesAsync(cancellationToken);
        var templateNames = await LoadTemplateNamesAsync(cancellationToken);

        var syncedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        var created = 0;
        var updated = 0;
        var errors = 0;
        var synced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var host in hosts)
        {
            try
            {
                var mainInterface = host.Interfaces
                    .FirstOrDefault(i => i.Main) ?? host.Interfaces.FirstOrDefault();
                var groups = host.GroupIds
                    .Select(id => groupNames.GetValueOrDefault(id))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToList();
                var templates = host.TemplateIds
                    .Select(id => templateNames.GetValueOrDefault(id))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToList();

                var existing = await hostsRepository.GetByHostIdAsync(
                    host.HostId, cancellationToken);

                if (existing is null)
                {
                    await hostsRepository.AddAsync(ZabbixHost.Create(
                        host.HostId, host.Host, host.Name,
                        mainInterface?.Ip, mainInterface?.Port > 0 ? mainInterface.Port : null,
                        host.Enabled, host.Discovered,
                        groups, templates, syncedAt), cancellationToken);
                    created++;
                }
                else
                {
                    existing.Update(
                        host.HostId, host.Host, host.Name,
                        mainInterface?.Ip, mainInterface?.Port > 0 ? mainInterface.Port : null,
                        host.Enabled, host.Discovered,
                        groups, templates, syncedAt);
                    updated++;
                }

                synced.Add(host.HostId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors++;
                logger.LogWarning(ex,
                    "Falha ao sincronizar host Zabbix {HostId} ({Host})",
                    host.HostId, host.Host);
            }
        }

        var missing = 0;
        foreach (var local in await hostsRepository.ListAsync(cancellationToken))
        {
            if (synced.Contains(local.HostId))
                continue;

            local.MarkMissing();
            missing++;
        }

        await hostsRepository.SaveChangesAsync(cancellationToken);
        await RecordSyncAsync(syncedAt, cancellationToken);

        logger.LogInformation(
            "Sync Zabbix concluído: {Created} criados, {Updated} atualizados, " +
            "{Missing} ausentes, {Errors} erros (versão {Version})",
            created, updated, missing, errors, version);

        return new ZabbixSyncResultDto(created, updated, missing, errors, version);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadGroupNamesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var groups = await client.ListHostGroupsAsync(cancellationToken);
            return groups.ToDictionary(g => g.GroupId, g => g.Name, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Grupos do Zabbix indisponíveis — hosts sincronizados sem nomes de grupos");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadTemplateNamesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var templates = await client.ListTemplatesAsync(cancellationToken);
            return templates.ToDictionary(t => t.TemplateId, t => t.Name, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Templates do Zabbix indisponíveis — hosts sincronizados sem nomes de templates");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private async Task RecordSyncAsync(DateTime syncedAt, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await settingsRepository.GetAsync(cancellationToken);
            if (setting is null)
                return;

            setting.RecordInventorySync(syncedAt);
            await settingsRepository.SaveAsync(setting, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Falha ao persistir data da última sincronização Zabbix");
        }
    }
}

// ── Query: hosts sincronizados (página Infraestrutura → Hosts monitorados) ──

/// <summary>
/// F27 Fase 3.3 (backend): hosts do Zabbix com busca por nome/IP/hostid,
/// filtros de estado/ausência e flag <see cref="ZabbixHostViewDto.HasAsset"/>
/// (match por IP → hostname no inventário, mesmo critério do webhook).
/// </summary>
public sealed record ZabbixHostViewDto(
    string HostId,
    string Host,
    string Name,
    string? Ip,
    int? Port,
    bool Enabled,
    bool Discovered,
    bool Missing,
    DateTime? MissingSinceUtc,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Templates,
    bool HasAsset,
    DateTime LastSyncedAtUtc);

public sealed record GetZabbixHostsQuery(
    string? Term = null,
    bool? Enabled = null,
    bool? Missing = null,
    int Page = 1,
    int PageSize = 20)
    : IQuery<PagedResult<ZabbixHostViewDto>>
{
}

public sealed class GetZabbixHostsQueryHandler(
    IZabbixHostsRepository hostsRepository,
    IDeviceRepository deviceRepository)
    : IRequestHandler<GetZabbixHostsQuery, PagedResult<ZabbixHostViewDto>>
{
    public async Task<PagedResult<ZabbixHostViewDto>> Handle(
        GetZabbixHostsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await hostsRepository.SearchAsync(
            request.Term, request.Enabled, request.Missing,
            page, pageSize, cancellationToken);

        var items = new List<ZabbixHostViewDto>(result.Items.Count);
        foreach (var host in result.Items)
            items.Add(await ToDtoAsync(host, cancellationToken));

        return new PagedResult<ZabbixHostViewDto>(
            items, result.TotalCount, result.Page, result.PageSize);
    }

    /// <summary>
    /// Match com o inventário: IP exato com fallback por hostname
    /// (mesmo critério do IngestZabbixProblemCommand — F4-T010).
    /// </summary>
    private async Task<ZabbixHostViewDto> ToDtoAsync(
        ZabbixHost host, CancellationToken cancellationToken)
    {
        var hasAsset = false;
        if (!string.IsNullOrWhiteSpace(host.Ip))
            hasAsset = await deviceRepository.GetByIpAsync(host.Ip, cancellationToken)
                is not null;

        if (!hasAsset)
            hasAsset = await deviceRepository.GetByNameAsync(host.Host, cancellationToken)
                is not null;

        return new ZabbixHostViewDto(
            host.HostId,
            host.Host,
            host.Name,
            host.Ip,
            host.Port,
            host.Enabled,
            host.Discovered,
            Missing: host.MissingSinceUtc is not null,
            host.MissingSinceUtc,
            host.ReadGroups(),
            host.ReadTemplates(),
            hasAsset,
            host.LastSyncedAtUtc);
    }
}
