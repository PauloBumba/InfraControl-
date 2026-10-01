using FluentAssertions;
using InfraControl.Api.Controllers.Admin;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        Mock<ISender> sender, params Claim[] claims)
    {
        var controller = new InvitationsController(sender.Object)
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
}
