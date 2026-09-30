using FluentAssertions;
using InfraControl.Api.Controllers.Integrations;
using InfraControl.Api.ViewModels.Integrations;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F16-T001/ADR-023: guard do token no controller — configuração vazia = 404
/// (integração desligada), token ausente/divergente = 401, token certo
/// encaminha o comando. O guard é a ÚNICA autenticação do endpoint (o comando
/// não implementa IAuthorizedRequest).
/// </summary>
public sealed class ZabbixIntegrationControllerTests
{
    private const string ValidToken = "token-segredo-zabbix";

    private static ZabbixIntegrationController Controller(ISender sender, string token)
        => new(sender, Options.Create(new ZabbixIntegrationOptions { Token = token }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private static IngestZabbixProblemViewModel ValidModel()
        => new() { Name = "CPU alta", Severity = 3, Status = "PROBLEM" };

    private static void SetToken(ZabbixIntegrationController controller, string? value)
    {
        if (value is not null)
            controller.HttpContext.Request.Headers["X-Zabbix-Token"] = value;
    }

    [Fact]
    public async Task Problems_EmptyConfiguredToken_ShouldReturn404Disabled()
    {
        var sender = new Mock<ISender>();
        var controller = Controller(sender.Object, token: string.Empty);

        var result = await controller.Problems(ValidModel(), CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var envelope = notFound.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.IsSuccess.Should().BeFalse();
        envelope.Errors.Should()
            .ContainSingle().Which.Should().Be("A integração com Zabbix está desativada.");
        sender.Verify(
            sender => sender.Send(
                It.IsAny<IngestZabbixProblemCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Problems_MissingHeader_ShouldReturn401InvalidToken()
    {
        var sender = new Mock<ISender>();
        var controller = Controller(sender.Object, ValidToken);

        var result = await controller.Problems(ValidModel(), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var envelope = unauthorized.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.IsSuccess.Should().BeFalse();
        envelope.Errors.Should()
            .ContainSingle().Which.Should().Be("Token do Zabbix inválido.");
        sender.Verify(
            sender => sender.Send(
                It.IsAny<IngestZabbixProblemCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Problems_WrongHeader_ShouldReturn401InvalidToken()
    {
        var sender = new Mock<ISender>();
        var controller = Controller(sender.Object, ValidToken);
        SetToken(controller, "token-errado");

        var result = await controller.Problems(ValidModel(), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var envelope = unauthorized.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        envelope.Errors.Should()
            .ContainSingle().Which.Should().Be("Token do Zabbix inválido.");
        sender.Verify(
            sender => sender.Send(
                It.IsAny<IngestZabbixProblemCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Problems_ValidToken_ShouldForwardCommandAndReturnEnvelope()
    {
        var expected = new ZabbixProblemResultDto(Guid.NewGuid(), "created");
        var sender = new Mock<ISender>();
        sender
            .Setup(sender => sender.Send(
                It.IsAny<IngestZabbixProblemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Controller(sender.Object, ValidToken);
        SetToken(controller, ValidToken);

        var result = await controller.Problems(ValidModel(), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeAssignableTo<ApiResponse<ZabbixProblemResultDto>>().Subject;
        envelope.IsSuccess.Should().BeTrue();
        envelope.Data.Should().Be(expected);
        sender.Verify(
            sender => sender.Send(
                It.IsAny<IngestZabbixProblemCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Problems_TokenComparison_ShouldBeCaseSensitive()
    {
        var sender = new Mock<ISender>();
        var controller = Controller(sender.Object, ValidToken);
        SetToken(controller, ValidToken.ToUpperInvariant());

        var result = await controller.Problems(ValidModel(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        sender.Verify(
            sender => sender.Send(
                It.IsAny<IngestZabbixProblemCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
