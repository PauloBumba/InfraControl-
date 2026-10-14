using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Network.Switches.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.SwitchPorts.Queries;

// Fase 07a — portas UniFi ao vivo compostas no SwitchesPage (sem ADR):
// resolve o MAC via ExternalDeviceReference (Source=UniFi) e devolve o
// port_table ao vivo. Sem referência → lista vazia (não é erro — o switch
// pode ser non-UniFi ou ainda não correlacionado). REGRA 8: nada é
// persistido, consulta sob demanda.

public sealed record GetSwitchUnifiPortsQuery(Guid SwitchId)
    : IQuery<IReadOnlyList<UnifiSwitchPortDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetSwitchUnifiPortsQueryHandler(
    GetSwitchQueryHandler switchQueryHandler,
    IExternalDeviceReferenceRepository referenceRepository,
    IUnifiApiClient unifiApiClient)
    : IRequestHandler<GetSwitchUnifiPortsQuery, IReadOnlyList<UnifiSwitchPortDto>>
{
    public async Task<IReadOnlyList<UnifiSwitchPortDto>> Handle(
        GetSwitchUnifiPortsQuery request, CancellationToken cancellationToken)
    {
        // Guard de type=Switch (404 pt-BR) — mesmo do SearchSwitchPortsQuery.
        await switchQueryHandler.Handle(
            new GetSwitchQuery(request.SwitchId), cancellationToken);

        var references = await referenceRepository.ListByDeviceIdAsync(
            request.SwitchId, cancellationToken);

        var unifi = references.FirstOrDefault(r => r.Source == ExternalDeviceSource.UniFi);
        if (unifi is null)
            return [];

        try
        {
            return await unifiApiClient.ListSwitchPortsAsync(
                unifi.ExternalId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            // Device sumiu do controller (removido/não adotado) — devolve
            // vazio em vez de 500; a UI mostra "sem portas UniFi".
            return [];
        }
    }
}
