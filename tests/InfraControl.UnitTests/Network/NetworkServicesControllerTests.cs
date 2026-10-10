using FluentAssertions;
using InfraControl.Api.Controllers.Network;
using InfraControl.Application.Network.Services;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F18-T005 (ADR-042): NetworkServicesController — envelope nos três
/// GETs de leitura e 503 + motivo (nunca 409 do handler global) quando
/// o equipamento/credencial/driver está indisponível ou não configurado.
/// </summary>
public sealed class NetworkServicesControllerTests
{
    private static NetworkServicesController Controller(ISender sender)
        => new(sender);

    [Fact]
    public async Task DhcpLeases_ShouldReturnOkEnvelope()
    {
        var expected = new[] { new DhcpLeaseDto("192.168.1.10", "aa:bb:cc:dd:ee:ff", 1) };
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<Application.Network.Services.Queries.GetDhcpLeasesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await Controller(sender.Object)
            .DhcpLeases(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should()
            .BeAssignableTo<ApiResponse<IReadOnlyList<DhcpLeaseDto>>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
        envelope.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Routes_ShouldReturnOkEnvelope()
    {
        var expected = new[]
        {
            new NetworkRouteDto(
                "0.0.0.0", "0.0.0.0", "192.168.1.1", 2, 10, "indirect", "ospf"),
        };
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<Application.Network.Services.Queries.GetRoutesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await Controller(sender.Object)
            .Routes(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should()
            .BeAssignableTo<ApiResponse<IReadOnlyList<NetworkRouteDto>>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
        envelope.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task FirewallRules_ShouldReturnOkEnvelope()
    {
        var expected = new[]
        {
            new FirewallRuleDto(
                "OUTSIDE-IN", "Extended", 10, "permit", "tcp", "any any eq 443"),
        };
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<Application.Network.Services.Queries.GetFirewallRulesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await Controller(sender.Object)
            .FirewallRules(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should()
            .BeAssignableTo<ApiResponse<IReadOnlyList<FirewallRuleDto>>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
        envelope.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task DhcpLeases_WhenNotConfigured_ShouldReturn503WithReason()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<Application.Network.Services.Queries.GetDhcpLeasesQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Serviços de rede não configurados (NetworkServices:Target)."));

        var result = await Controller(sender.Object)
            .DhcpLeases(CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable,
            "sem equipamento é indisponibilidade, não conflito (409 do handler global)");
        var envelope = status.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.IsSuccess.Should().BeFalse();
        envelope.Errors.Should().ContainSingle()
            .Which.Should().Contain("não configurados");
    }

    [Fact]
    public async Task Routes_WhenSnmpFails_ShouldReturn503WithReason()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<Application.Network.Services.Queries.GetRoutesQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Falha ao consultar 192.168.1.1 via SNMP (1.3.6.1.2.1.4.21.1): timeout."));

        var result = await Controller(sender.Object)
            .Routes(CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var envelope = status.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.Errors.Should().ContainSingle().Which.Should().Contain("via SNMP");
    }

    [Fact]
    public async Task FirewallRules_WhenVendorUnknown_ShouldReturn503ListingSupported()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<Application.Network.Services.Queries.GetFirewallRulesQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Vendor 'juniper' sem leitor de firewall — suportados: cisco."));

        var result = await Controller(sender.Object)
            .FirewallRules(CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var envelope = status.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.Errors.Should().ContainSingle().Which.Should().Contain("suportados: cisco");
    }
}
