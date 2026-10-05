using FluentAssertions;
using InfraControl.Api.Controllers.Integrations;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F20-T001/ADR-028 §5: POST /api/v1/integrations/unifi/sync encaminha o
/// comando (roles ficam no IAuthorizedRequest do command — nenhum literal
/// de role no controller, regra do arch test) e o GET /health é sondagem
/// ao vivo do Controller (503 quando indisponível/desativado).
/// </summary>
public sealed class UnifiIntegrationControllerTests
{
    private static UnifiIntegrationController Controller(
        ISender sender, IUnifiApiClient? client = null)
        => new(sender, client ?? Mock.Of<IUnifiApiClient>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    [Fact]
    public async Task Sync_ShouldForwardCommandAndReturnEnvelope()
    {
        var expected = new UnifiSyncResultDto(Created: 2, Updated: 1, References: 2, Errors: 0);
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<SyncUnifiInventoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Controller(sender.Object);

        var result = await controller.Sync(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeAssignableTo<ApiResponse<UnifiSyncResultDto>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
        envelope.Data.Should().Be(expected);
        sender.Verify(s => s.Send(
            It.IsAny<SyncUnifiInventoryCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Health_WhenControllerUp_ShouldReturnOk()
    {
        var client = new Mock<IUnifiApiClient>();
        client.Setup(c => c.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var controller = Controller(Mock.Of<ISender>(), client.Object);

        var result = await controller.Health(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Health_WhenControllerDown_ShouldReturn503()
    {
        var client = new Mock<IUnifiApiClient>();
        client.Setup(c => c.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var controller = Controller(Mock.Of<ISender>(), client.Object);

        var result = await controller.Health(CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var envelope = status.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.IsSuccess.Should().BeFalse();
        envelope.Errors.Should().ContainSingle()
            .Which.Should().Contain("UniFi Controller indisponível");
    }
}
