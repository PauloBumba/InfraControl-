using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.SwitchPorts.Queries;

// Fase 07b — interfaces SNMP monitoradas pelo Zabbix para um switch.
// Resolve referência Zabbix → hostId → ListItemsAsync → filtra itens de
// interface (ifOperStatus/ifAdminStatus/ifInOctets/ifOutOctets/ifSpeed) e
// devolve o nome da interface extraído da key. Sem referência ou Zabbix
// não configurado → lista vazia (não é erro).

public sealed record ZabbixInterfaceItemDto(
    string ItemId,
    string Name,
    string Key,
    string InterfaceName,
    bool Enabled,
    bool Unsupported);

public sealed record GetSwitchZabbixInterfacesQuery(Guid SwitchId)
    : IQuery<IReadOnlyList<ZabbixInterfaceItemDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetSwitchZabbixInterfacesQueryHandler(
    GetSwitchQueryHandler switchQueryHandler,
    IExternalDeviceReferenceRepository referenceRepository,
    IZabbixHostsRepository zabbixHostsRepository,
    IZabbixClient zabbixClient)
    : IRequestHandler<GetSwitchZabbixInterfacesQuery, IReadOnlyList<ZabbixInterfaceItemDto>>
{
    private static readonly string[] InterfaceKeyMarkers =
    [
        "ifOperStatus", "ifAdminStatus",
        "ifInOctets", "ifOutOctets", "ifInErrors", "ifOutErrors",
        "ifSpeed", "ifAlias", "ifName",
        "net.if.status", "net.if.in", "net.if.out", "net.if.speed",
    ];

    public async Task<IReadOnlyList<ZabbixInterfaceItemDto>> Handle(
        GetSwitchZabbixInterfacesQuery request, CancellationToken cancellationToken)
    {
        // Guard de type=Switch (404 pt-BR).
        await switchQueryHandler.Handle(
            new GetSwitchQuery(request.SwitchId), cancellationToken);

        if (!zabbixClient.IsConfigured)
            return [];

        var references = await referenceRepository.ListByDeviceIdAsync(
            request.SwitchId, cancellationToken);
        var zabbixRef = references.FirstOrDefault(r => r.Source == ExternalDeviceSource.Zabbix);
        if (zabbixRef is null)
            return [];

        var host = await zabbixHostsRepository.GetByHostIdAsync(
            zabbixRef.ExternalId, cancellationToken);
        if (host is null || host.MissingSinceUtc is not null)
            return [];

        var items = await zabbixClient.ListItemsAsync(host.HostId, cancellationToken);

        return items
            .Where(item => InterfaceKeyMarkers.Any(marker =>
                item.Key.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(item => new ZabbixInterfaceItemDto(
                item.ItemId,
                item.Name,
                item.Key,
                ExtractInterfaceName(item.Key),
                item.Enabled,
                item.Unsupported))
            .OrderBy(item => item.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extrai o nome da interface da key Zabbix: `ifOperStatus[ge-0/0/0]`
    /// → `ge-0/0/0`; `ifOperStatus["Gi0/1"]` → `Gi0/1`; sem brackets →
    /// devolve a key inteira.
    /// </summary>
    private static string ExtractInterfaceName(string key)
    {
        var open = key.IndexOf('[');
        var close = key.LastIndexOf(']');
        if (open < 0 || close <= open)
            return key;

        var inner = key[(open + 1)..close].Trim();
        // Remove aspas duplas comuns em keys Zabbix: ["Gi0/1"].
        return inner.Trim('"');
    }
}
