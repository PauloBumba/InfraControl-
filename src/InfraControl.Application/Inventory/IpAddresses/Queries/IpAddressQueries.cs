using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.IpAddresses.Queries;

// Sem cache (IQuery direto): inventário muda a cada mutation — mesmo motivo
// documentado em LocationQueries.

public sealed record SearchIpAddressesQuery(
    Guid? InterfaceId = null,
    Guid? DeviceId = null,
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<IpAddressDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchIpAddressesQueryHandler(IIpAddressRepository repository)
    : IRequestHandler<SearchIpAddressesQuery, PagedResult<IpAddressDto>>
{
    public async Task<PagedResult<IpAddressDto>> Handle(
        SearchIpAddressesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var term = string.IsNullOrWhiteSpace(request.Term) ? null : request.Term.Trim();

        var result = await repository.SearchAsync(
            request.InterfaceId, request.DeviceId, term, page, pageSize, cancellationToken);

        return new PagedResult<IpAddressDto>(
            result.Items
                .Select(entity => new IpAddressDto(entity.Id, entity.InterfaceId, entity.Ip))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

public sealed record GetIpAddressQuery(Guid Id) : IQuery<IpAddressDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetIpAddressQueryHandler(IIpAddressRepository repository)
    : IRequestHandler<GetIpAddressQuery, IpAddressDto>
{
    public async Task<IpAddressDto> Handle(GetIpAddressQuery request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"IP '{request.Id}' não encontrado.");

        return new IpAddressDto(entity.Id, entity.InterfaceId, entity.Ip);
    }
}

// ── F29-4: detecção de conflito de IP (read-model, nada persistido) ──

/// <summary>Uma reclamação de um IP por uma identidade (device ou host Zabbix).</summary>
public sealed record IpConflictClaimDto(
    string Source,      // "Interface" | "Management" | "ZabbixHost"
    string OwnerLabel,  // nome do device ou do host Zabbix
    Guid? DeviceId);    // device dono (null = host Zabbix não correlacionado)

/// <summary>IP reclamado por 2+ identidades distintas (conflito).</summary>
public sealed record IpConflictDto(
    string Ip,
    IReadOnlyList<IpConflictClaimDto> Claims);

/// <summary>
/// F29-4: cruza as fontes PERSISTIDAS de IP (interface de device, IP de
/// gestão do device e host Zabbix) e devolve só os IPs reclamados por 2+
/// identidades distintas. Identidade = DeviceId (interface/gestão/Zabbix
/// correlacionado via <c>ExternalDeviceReference</c>) ou pseudo-chave
/// <c>zabbix:hostid</c> p/ host não correlacionado. Um device que tem o
/// mesmo IP na interface E na gestão NÃO é conflito (mesma identidade); um
/// host Zabbix correlacionado ao device dono do IP também não. Read-model
/// puro — não persiste telemetria (REGRA 8) e não toca a unicidade UNIQUE
/// de <c>ip_addresses</c>.
/// </summary>
public sealed record GetIpConflictsQuery
    : IQuery<IReadOnlyList<IpConflictDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetIpConflictsQueryHandler(
    IIpAddressRepository ipAddressRepository,
    IDeviceRepository deviceRepository,
    IZabbixHostsRepository zabbixHostsRepository,
    IExternalDeviceReferenceRepository referenceRepository)
    : IRequestHandler<GetIpConflictsQuery, IReadOnlyList<IpConflictDto>>
{
    public async Task<IReadOnlyList<IpConflictDto>> Handle(
        GetIpConflictsQuery request, CancellationToken cancellationToken)
    {
        // identidade (device) → nome, p/ rótulo das reclamações
        var devices = await deviceRepository.GetAllOrderedByNameAsync(cancellationToken);
        var deviceNames = devices.ToDictionary(d => d.Id, d => d.Name);

        // ip → (identidade → reclamação); uma identidade conta 1× por IP
        var byIp = new Dictionary<string, Dictionary<string, IpConflictClaimDto>>(
            StringComparer.Ordinal);

        void Claim(string ip, string identity, IpConflictClaimDto dto)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return;
            var canonical = ip.Trim();
            if (!byIp.TryGetValue(canonical, out var identities))
            {
                identities = new Dictionary<string, IpConflictClaimDto>(StringComparer.Ordinal);
                byIp[canonical] = identities;
            }
            identities[identity] = dto;
        }

        // 1) IPs de interface (device dono via join)
        foreach (var claim in await ipAddressRepository.ListInterfaceClaimsAsync(cancellationToken))
            Claim(claim.Ip, claim.DeviceId.ToString(),
                new IpConflictClaimDto("Interface",
                    deviceNames.GetValueOrDefault(claim.DeviceId) ?? claim.DeviceId.ToString(),
                    claim.DeviceId));

        // 2) IP de gestão do device
        foreach (var device in devices)
        {
            if (string.IsNullOrWhiteSpace(device.Ip))
                continue;
            Claim(device.Ip!, device.Id.ToString(),
                new IpConflictClaimDto("Management", device.Name, device.Id));
        }

        // 3) hosts Zabbix (identidade = device correlacionado ou pseudo zabbix:hostid)
        var zabbixLinks = (await referenceRepository.ListBySourceAsync(
                ExternalDeviceSource.Zabbix, cancellationToken))
            .ToDictionary(r => r.ExternalId, r => r.DeviceId, StringComparer.Ordinal);

        foreach (var host in await zabbixHostsRepository.ListAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(host.Ip))
                continue;

            zabbixLinks.TryGetValue(host.HostId, out var linkedId);
            var deviceId = linkedId == Guid.Empty ? (Guid?)null : linkedId;
            var identity = deviceId?.ToString() ?? $"zabbix:{host.HostId}";
            var label = deviceId is not null && deviceNames.TryGetValue(deviceId.Value, out var name)
                ? name
                : host.Name;

            Claim(host.Ip!, identity,
                new IpConflictClaimDto("ZabbixHost", label, deviceId));
        }

        // conflito = 2+ identidades distintas no mesmo IP
        return byIp
            .Where(entry => entry.Value.Count >= 2)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new IpConflictDto(
                entry.Key,
                entry.Value.Values
                    .OrderBy(c => c.Source, StringComparer.Ordinal)
                    .ThenBy(c => c.OwnerLabel, StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }
}
