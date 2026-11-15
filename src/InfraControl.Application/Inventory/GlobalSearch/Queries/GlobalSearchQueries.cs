using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.IpAddresses.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Inventory.MacAddresses.Ports;
using InfraControl.Application.Inventory.NetworkInterfaces.Ports;
using InfraControl.Application.Inventory.Racks.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Inventory.GlobalSearch.Queries;

// F2-T007: busca global somente leitura — agrega as 6 fontes do inventário
// reusando os SearchAsync existentes (sem SQL próprio, sem migração). Erro em
// pt-BR nasce aqui; o transporte embrulha no envelope via GlobalExceptionHandler.

public sealed record GlobalSearchQuery(
    string? Term,
    int Limit = 5) : IQuery<GlobalSearchResultDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GlobalSearchQueryHandler(
    ILocationRepository locations,
    IRackRepository racks,
    IDeviceRepository devices,
    INetworkInterfaceRepository interfaces,
    IMacAddressRepository macs,
    IIpAddressRepository ips,
    Network.Explorer.SearchMacLookupQueryHandler macLookup) : IRequestHandler<GlobalSearchQuery, GlobalSearchResultDto>
{
    public async Task<GlobalSearchResultDto> Handle(
        GlobalSearchQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Term))
            throw new ArgumentException("O termo de busca é obrigatório.", nameof(request.Term));

        var term = request.Term.Trim();
        var limit = Math.Clamp(request.Limit, 1, 50);

        var items = new List<GlobalSearchItemDto>();
        var totalCount = 0;

        // Ordem fixa (local → rack → device → interface → mac → ip) para
        // resposta determinística; limit vale POR fonte, total reflete tudo.
        var locationResult = await locations.SearchAsync(term, 1, limit, cancellationToken);
        items.AddRange(locationResult.Items.Select(entity =>
            new GlobalSearchItemDto("location", entity.Id, entity.Name, null)));
        totalCount += locationResult.TotalCount;

        var rackResult = await racks.SearchAsync(null, term, 1, limit, cancellationToken);
        items.AddRange(rackResult.Items.Select(entity =>
            new GlobalSearchItemDto("rack", entity.Id, entity.Name, null)));
        totalCount += rackResult.TotalCount;

        var deviceResult = await devices.SearchAsync(
            null, null, null, term, 1, limit, cancellationToken);
        items.AddRange(deviceResult.Items.Select(entity =>
            new GlobalSearchItemDto(
                "device", entity.Id, entity.Name,
                string.IsNullOrWhiteSpace(entity.Ip) ? null : entity.Ip)));
        totalCount += deviceResult.TotalCount;

        var interfaceResult = await interfaces.SearchAsync(null, term, 1, limit, cancellationToken);
        items.AddRange(interfaceResult.Items.Select(entity =>
            new GlobalSearchItemDto("interface", entity.Id, entity.Name, null)));
        totalCount += interfaceResult.TotalCount;

        var macResult = await macs.SearchAsync(null, null, term, 1, limit, cancellationToken);
        items.AddRange(macResult.Items.Select(entity =>
            new GlobalSearchItemDto("mac", entity.Id, entity.Mac, null)));
        totalCount += macResult.TotalCount;

        var ipResult = await ips.SearchAsync(null, null, term, 1, limit, cancellationToken);
        items.AddRange(ipResult.Items.Select(entity =>
            new GlobalSearchItemDto("ip", entity.Id, entity.Ip, null)));
        totalCount += ipResult.TotalCount;

        // Network Explorer F3 — fonte relacional: evidências de discovery
        // (tabela MAC/ARP/DHCP/LLDP + AP Wi-Fi). Só adiciona item quando há
        // evidência real; nunca inventa. Erro aqui não derruba a busca.
        try
        {
            var lookup = await macLookup.Handle(
                new Network.Explorer.SearchMacLookupQuery(term), cancellationToken);
            if (lookup.Sightings.Count > 0)
            {
                // Id sintético estável (hash do termo) — a navegação real vai
                // em Target (rota do Explorer com o termo original).
                var anchor = lookup.MatchedMac ?? lookup.Ip ?? lookup.Hostname ?? term;
                var context = string.Join(" · ", new[]
                {
                    lookup.Ip,
                    lookup.Hostname,
                    lookup.Vlan is { } v ? $"VLAN {v}" : null,
                    lookup.WifiApName is { } ap ? $"Wi-Fi: {ap}" : null,
                }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var hash = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(term));
                items.Add(new GlobalSearchItemDto(
                    "network",
                    new Guid(hash[..16]),
                    anchor,
                    $"{lookup.Sightings.Count} evidência(s) de rede{(context.Length > 0 ? $" · {context}" : "")}",
                    $"/network/explorer?term={Uri.EscapeDataString(term)}"));
                totalCount += 1;
            }
        }
        catch
        {
            // fonte auxiliar — falha não bloqueia o restante da busca
        }

        return new GlobalSearchResultDto(items, totalCount);
    }
}
