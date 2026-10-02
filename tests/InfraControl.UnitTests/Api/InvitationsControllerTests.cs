using FluentAssertions;
using InfraControl.Api.Controllers.Admin;
using InfraControl.Api.Services;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// <c>POST /api/v1/admin/invitations</c> derivava o convidante de
/// <c>FindFirst("sub")</c>/<c>"nameidentifier"</c>, mas a sessão carrega
/// <see cref="ClaimTypes.NameIdentifier"/>: todo convite era recusado com 401
/// para um admin autenticado.
/// </summary>
public sealed class InvitationsControllerTests
{
    private static InvitationsController ControllerWith(
        Mock<ISender> sender, FrontendOptions? frontend = null, params Claim[] claims)
    {
        var controller = new InvitationsController(
            sender.Object, Options.Create(frontend ?? new FrontendOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(claims, authenticationType: "Test"))
                }
            }
        };

        return controller;
    }

    [Fact]
    public async Task Create_Should_Send_The_Session_User_As_The_Inviter()
    {
        var inviterId = Guid.NewGuid();
        var captured = default(CreateInvitationCommand?);

        var sender = new Mock<ISender>();
        sender
            .Setup(mediator => mediator.Send(It.IsAny<CreateInvitationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateInvitationResult>, CancellationToken>(
                (command, _) => captured = (CreateInvitationCommand)command)
            .ReturnsAsync(new CreateInvitationResult(Guid.NewGuid(), "token"));

        var controller = ControllerWith(
            sender,
            null,
            new Claim(ClaimTypes.NameIdentifier, inviterId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString()));

        var model = new CreateInvitationViewModel(
            "convite@exemplo.test", [UserRole.Viewer], 7);

        var result = await controller.Create(model, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        captured.Should().NotBeNull();
        captured!.CreatedBy.Should().Be(inviterId);
    }

    [Fact]
    public async Task Create_Should_Be_Unauthorized_Without_A_Session_Claim()
    {
        var sender = new Mock<ISender>();
        var controller = ControllerWith(sender);

        var model = new CreateInvitationViewModel(
            "convite@exemplo.test", [UserRole.Viewer], 7);

        var result = await controller.Create(model, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        sender.Verify(
            mediator => mediator.Send(It.IsAny<CreateInvitationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Decisão do usuário (01/10/2026): a origem dos links de e-mail vem da
    /// config <c>Frontend:BaseUrl</c> — em dev a API vista pelo proxy do vite
    /// é <c>localhost:7237</c>, que não serve o SPA; com a config o link nasce
    /// em <c>http://localhost:3000</c>. Trailing slash é cortado na origem.
    /// </summary>
    [Fact]
    public async Task Create_Should_Use_The_Configured_Frontend_BaseUrl()
    {
        var captured = default(CreateInvitationCommand?);

        var sender = new Mock<ISender>();
        sender
            .Setup(mediator => mediator.Send(It.IsAny<CreateInvitationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateInvitationResult>, CancellationToken>(
                (command, _) => captured = (CreateInvitationCommand)command)
            .ReturnsAsync(new CreateInvitationResult(Guid.NewGuid(), "token"));

        var controller = ControllerWith(
            sender,
            new FrontendOptions { BaseUrl = "http://localhost:3000/" },
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString()));

        await controller.Create(
            new CreateInvitationViewModel("convite@exemplo.test", [UserRole.Viewer], 7),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.BaseUrl.Should().Be("http://localhost:3000");
    }

    /// <summary>
    /// Sem config (produção com nginx same-origin) a origem continua sendo a
    /// da própria request — comportamento anterior preservado.
    /// </summary>
    [Fact]
    public async Task Create_Should_Fall_Back_To_The_Request_Origin_When_Config_Is_Empty()
    {
        var captured = default(CreateInvitationCommand?);

        var sender = new Mock<ISender>();
        sender
            .Setup(mediator => mediator.Send(It.IsAny<CreateInvitationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateInvitationResult>, CancellationToken>(
                (command, _) => captured = (CreateInvitationCommand)command)
            .ReturnsAsync(new CreateInvitationResult(Guid.NewGuid(), "token"));

        var controller = ControllerWith(
            sender,
            new FrontendOptions { BaseUrl = "   " },
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString()));
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("app.intra.example");

        await controller.Create(
            new CreateInvitationViewModel("convite@exemplo.test", [UserRole.Viewer], 7),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.BaseUrl.Should().Be("https://app.intra.example");
    }
}
