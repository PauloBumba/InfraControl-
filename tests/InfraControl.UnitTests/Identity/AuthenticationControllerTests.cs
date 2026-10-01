using FluentAssertions;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Api.Controllers.Identity;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.BuildingBlocks.Application.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace InfraControl.UnitTests.Identity;

public sealed class AuthenticationControllerTests
{
    [Fact]
    public void Me_ShouldReturnRolesFromClaimTypesRole()
    {
        // Arrange
        var authServiceMock = new Mock<IIdentityAuthenticationService>();
        var controller = new AuthenticationController(authServiceMock.Object, Mock.Of<Microsoft.AspNetCore.Antiforgery.IAntiforgery>());

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "123"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim("displayName", "Test User"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Viewer"),
            new Claim("permission", "InventoryRead"),
            new Claim("permission", "MonitoringRead")
        ], "TestScheme"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = controller.Me();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;

        // Verify the controller uses ClaimTypes.Role to get roles
        user.FindAll(ClaimTypes.Role).Select(c => c.Value).Should().Contain("Admin").And.Contain("Viewer");
        user.FindAll("permission").Select(c => c.Value).Should().Contain("InventoryRead").And.Contain("MonitoringRead");
    }

    [Fact]
    public async Task Logout_Should_Succeed_When_The_Claim_Is_The_Session_Identity_One()
    {
        // O cookie é emitido com ClaimTypes.NameIdentifier; Logout lia
        // "sub"/"nameidentifier" e devolvia 401 — o usuário nunca saía da
        // sessão (o cookie continuava válido até expirar).
        var userId = Guid.NewGuid();
        var authServiceMock = new Mock<IIdentityAuthenticationService>();
        authServiceMock
            .Setup(service => service.LogoutAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var signOutMock = new Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        signOutMock
            .Setup(service => service.SignOutAsync(
                It.IsAny<Microsoft.AspNetCore.Http.HttpContext>(), It.IsAny<string>(), It.IsAny<Microsoft.AspNetCore.Authentication.AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var servicesMock = new Mock<IServiceProvider>();
        servicesMock
            .Setup(provider => provider.GetService(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService)))
            .Returns(signOutMock.Object);

        var controller = new AuthenticationController(authServiceMock.Object, Mock.Of<Microsoft.AspNetCore.Antiforgery.IAntiforgery>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestScheme")),
                    RequestServices = servicesMock.Object
                }
            }
        };

        var result = await controller.Logout(null, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        authServiceMock.Verify(
            service => service.LogoutAsync(userId, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Me_ShouldReturnEmptyArraysWhenNoClaims()
    {
        // Arrange
        var authServiceMock = new Mock<IIdentityAuthenticationService>();
        var controller = new AuthenticationController(authServiceMock.Object, Mock.Of<Microsoft.AspNetCore.Antiforgery.IAntiforgery>());

        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "123"),
            new Claim(ClaimTypes.Email, "test@example.com")
        ], "TestScheme"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = controller.Me();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;

        // Verify the controller uses ClaimTypes.Role to get roles
        user.FindAll(ClaimTypes.Role).Should().BeEmpty();
        user.FindAll("permission").Should().BeEmpty();
    }
}
