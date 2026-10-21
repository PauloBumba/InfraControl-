using FluentAssertions;
using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers.Integrations;
using InfraControl.Application.Integrations.WhatsApp;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InfraControl.UnitTests.Integrations.WhatsApp;

public sealed class WhatsAppIntegrationControllerTests
{
    private readonly Mock<ISender> _sender = new();

    private WhatsAppIntegrationController CreateController() => new(_sender.Object);

    [Fact]
    public void Controller_ShouldHaveAdministerPolicy()
    {
        var type = typeof(WhatsAppIntegrationController);
        var attr = (AuthorizeAttribute?)Attribute.GetCustomAttribute(type, typeof(AuthorizeAttribute));

        attr.Should().NotBeNull();
        attr!.Policy.Should().Be(AuthorizationPolicies.Administer);
    }

    [Fact]
    public async Task GetConfig_ShouldCallSenderAndReturnOk()
    {
        var dto = new WhatsAppSettingsDto(
            "http://url:8080", true, "Admin", "grp@g.us", null, "open", "database",
            null, false, null, null);
        _sender.Setup(s => s.Send(It.IsAny<GetWhatsAppSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = CreateController();
        var result = await controller.GetConfig(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponse<WhatsAppSettingsDto>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
        envelope.Data.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task SaveConfig_ShouldCallSenderAndReturnOk()
    {
        var dto = new WhatsAppSettingsDto(
            "http://url:8080", true, "Admin", "grp@g.us", null, "open", "database",
            null, false, null, null);
        _sender.Setup(s => s.Send(It.IsAny<SaveWhatsAppSettingsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = CreateController();
        var request = new SaveWhatsAppSettingsRequest("http://url:8080", "Admin", "key", "grp@g.us", null);
        var result = await controller.SaveConfig(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponse<WhatsAppSettingsDto>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_ShouldCallSenderAndReturnOk()
    {
        var qrDto = new WhatsAppQrDto([1, 2, 3], "code", "connecting");
        _sender.Setup(s => s.Send(It.IsAny<GenerateWhatsAppQrCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(qrDto);

        var controller = CreateController();
        var result = await controller.Connect(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponse<WhatsAppQrDto>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
        envelope.Data.Should().BeSameAs(qrDto);
    }
}
