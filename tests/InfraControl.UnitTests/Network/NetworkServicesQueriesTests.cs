using FluentAssertions;
using InfraControl.Application.Network.Services;
using InfraControl.Application.Network.Services.Ports;
using InfraControl.Application.Network.Services.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using Moq;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F18-T005 (ADR-042): as três queries de leitura dos serviços de rede
/// encaminham para o reader e exigem apenas papel autenticado
/// (RoleAccess.Read — nível único ADR-030 §3).
/// </summary>
public sealed class NetworkServicesQueriesTests
{
    [Fact]
    public async Task GetDhcpLeasesQuery_ShouldDelegateToReaderWithReadAccess()
    {
        var expected = new[] { new DhcpLeaseDto("192.168.1.10", "aa:bb:cc:dd:ee:ff", 1) };
        var reader = new Mock<INetworkServicesReader>();
        reader.Setup(r => r.GetDhcpLeasesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await new GetDhcpLeasesQueryHandler(reader.Object)
            .Handle(new GetDhcpLeasesQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        new GetDhcpLeasesQuery().RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
        new GetDhcpLeasesQuery().Should().BeAssignableTo<IAuthorizedRequest>();
    }

    [Fact]
    public async Task GetRoutesQuery_ShouldDelegateToReaderWithReadAccess()
    {
        var expected = new[]
        {
            new NetworkRouteDto(
                "0.0.0.0", "0.0.0.0", "192.168.1.1", 2, 10, "indirect", "ospf"),
        };
        var reader = new Mock<INetworkServicesReader>();
        reader.Setup(r => r.GetRoutesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await new GetRoutesQueryHandler(reader.Object)
            .Handle(new GetRoutesQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        new GetRoutesQuery().RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
    }

    [Fact]
    public async Task GetFirewallRulesQuery_ShouldDelegateToReaderWithReadAccess()
    {
        var expected = new[]
        {
            new FirewallRuleDto(
                "OUTSIDE-IN", "Extended", 10, "permit", "tcp", "any any eq 443"),
        };
        var reader = new Mock<INetworkServicesReader>();
        reader.Setup(r => r.GetFirewallRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await new GetFirewallRulesQueryHandler(reader.Object)
            .Handle(new GetFirewallRulesQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        new GetFirewallRulesQuery().RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
    }
}
