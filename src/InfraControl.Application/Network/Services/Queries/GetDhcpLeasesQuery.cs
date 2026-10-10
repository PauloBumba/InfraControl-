using InfraControl.Application.Network.Services.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Services.Queries;

/// <summary>
/// F18-T005 (ADR-042): leases/ARP ativos do equipamento configurado
/// (SNMP read-only, snapshot ao vivo — sem persistência).
/// </summary>
public sealed record GetDhcpLeasesQuery
    : IQuery<IReadOnlyList<DhcpLeaseDto>>, IAuthorizedRequest
{
    /// <summary>Leitura operacional: qualquer papel autenticado.</summary>
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetDhcpLeasesQueryHandler(INetworkServicesReader reader)
    : IRequestHandler<GetDhcpLeasesQuery, IReadOnlyList<DhcpLeaseDto>>
{
    public Task<IReadOnlyList<DhcpLeaseDto>> Handle(
        GetDhcpLeasesQuery request, CancellationToken cancellationToken)
        => reader.GetDhcpLeasesAsync(cancellationToken);
}
