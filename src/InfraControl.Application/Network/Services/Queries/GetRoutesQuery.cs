using InfraControl.Application.Network.Services.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Services.Queries;

/// <summary>
/// F18-T005 (ADR-042): tabela de rotas IP do equipamento configurado
/// (SNMP read-only, snapshot ao vivo — sem persistência).
/// </summary>
public sealed record GetRoutesQuery
    : IQuery<IReadOnlyList<NetworkRouteDto>>, IAuthorizedRequest
{
    /// <summary>Leitura operacional: qualquer papel autenticado.</summary>
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetRoutesQueryHandler(INetworkServicesReader reader)
    : IRequestHandler<GetRoutesQuery, IReadOnlyList<NetworkRouteDto>>
{
    public Task<IReadOnlyList<NetworkRouteDto>> Handle(
        GetRoutesQuery request, CancellationToken cancellationToken)
        => reader.GetRoutesAsync(cancellationToken);
}
