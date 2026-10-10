using InfraControl.Application.Network.Services.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Network.Services.Queries;

/// <summary>
/// F18-T005 (ADR-042): regras de firewall (ACLs) do equipamento
/// configurado — comando show de leitura por vendor (v1: Cisco), texto
/// cru do device. Snapshot ao vivo — sem persistência.
/// </summary>
public sealed record GetFirewallRulesQuery
    : IQuery<IReadOnlyList<FirewallRuleDto>>, IAuthorizedRequest
{
    /// <summary>Leitura operacional: qualquer papel autenticado.</summary>
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetFirewallRulesQueryHandler(INetworkServicesReader reader)
    : IRequestHandler<GetFirewallRulesQuery, IReadOnlyList<FirewallRuleDto>>
{
    public Task<IReadOnlyList<FirewallRuleDto>> Handle(
        GetFirewallRulesQuery request, CancellationToken cancellationToken)
        => reader.GetFirewallRulesAsync(cancellationToken);
}
