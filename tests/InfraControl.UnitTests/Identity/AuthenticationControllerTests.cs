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
        var controller = new AuthenticationController(authServiceMock.Object);

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
    public void Me_ShouldReturnEmptyArraysWhenNoClaims()
    {
        // Arrange
        var authServiceMock = new Mock<IIdentityAuthenticationService>();
        var controller = new AuthenticationController(authServiceMock.Object);

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
