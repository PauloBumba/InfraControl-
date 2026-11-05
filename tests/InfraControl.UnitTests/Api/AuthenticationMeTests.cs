using System.Security.Claims;
using FluentAssertions;
using InfraControl.Api.Controllers.Identity;
using InfraControl.Api.Services;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.Infrastructure.Auth;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// F1-T014/ADR-016: <c>GET /api/v1/auth/me</c> devolvia <c>roles</c> vazio
/// porque lia a claim com o literal "role" enquanto o login emite
/// <see cref="ClaimTypes.Role"/>. O frontend usa <c>roles</c> para decidir o
/// que exibir, então o bug era silencioso. Este teste trava o tipo de claim.
/// </summary>
public sealed class AuthenticationMeTests
{
    private static AuthenticationController ControllerWith(params Claim[] claims)
        => ControllerWithAntiforgery(Mock.Of<IAntiforgery>(), claims);

    private static AuthenticationController ControllerWithAntiforgery(
        IAntiforgery antiforgery, params Claim[] claims)
    {
        var controller = new AuthenticationController(Mock.Of<IIdentityAuthenticationService>(), antiforgery, Options.Create(new FrontendOptions()), Options.Create(new GoogleAuthOptions()))
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

    // o payload de Me() é anônimo; navega por reflexão para não acoplar o
    // teste à forma do envelope
    private static string[] RolesIn(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value!.GetType().GetProperty("Data")!.GetValue(ok.Value);

        return (string[])data!.GetType().GetProperty("roles")!.GetValue(data)!;
    }

    private static string? UserIdIn(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value!.GetType().GetProperty("Data")!.GetValue(ok.Value);

        return (string?)data!.GetType().GetProperty("userId")!.GetValue(data);
    }

    private static string? FullNameIn(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value!.GetType().GetProperty("Data")!.GetValue(ok.Value);

        return (string?)data!.GetType().GetProperty("fullName")!.GetValue(data);
    }

    [Fact]
    public void Me_Should_Return_Roles_From_ClaimTypesRole()
    {
        var controller = ControllerWith(
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Email, "admin@exemplo.test"),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString()));

        RolesIn(controller.Me()).Should().BeEquivalentTo([UserRole.Admin.ToString()]);
    }

    [Fact]
    public void Me_Should_Return_All_Roles_Of_A_Multi_Role_User()
    {
        var controller = ControllerWith(
            new Claim(ClaimTypes.Role, UserRole.Viewer.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Operator.ToString()));

        RolesIn(controller.Me()).Should().BeEquivalentTo(
            [UserRole.Viewer.ToString(), UserRole.Operator.ToString()]);
    }

    [Fact]
    public void Me_Should_Not_Read_A_Literal_Role_Claim()
    {
        // claim errada ("role" em vez de ClaimTypes.Role) não pode alimentar
        // a resposta: é exatamente o bug que foi corrigido
        var controller = ControllerWith(new Claim("role", UserRole.Admin.ToString()));

        RolesIn(controller.Me()).Should().BeEmpty();
    }

    [Fact]
    public void Me_Should_Return_Empty_Roles_When_User_Has_No_Role()
    {
        var controller = ControllerWith(
            new Claim(ClaimTypes.NameIdentifier, "user-123"));

        RolesIn(controller.Me()).Should().BeEmpty();
    }

    [Fact]
    public void Me_Should_Return_UserId_From_The_Session_Identity_Claim()
    {
        // O cookie nasce em CreatePrincipal com ClaimTypes.NameIdentifier (URI
        // longa). Me() lia "sub"/"nameidentifier" e nunca achava: userId saía
        // null e o frontend (auth.ts id: payload.userId) ficava sem identidade.
        var userId = Guid.NewGuid().ToString();
        var controller = ControllerWith(
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, "admin@exemplo.test"));

        UserIdIn(controller.Me()).Should().Be(userId);
    }

    [Fact]
    public void Me_Should_Return_Null_UserId_When_Unauthenticated_But_Not_Crash()
    {
        // /auth/me é [Authorize]; aqui só garante que a leitura da claim é
        // tolerante (sem claim de identidade → null, não exceção).
        UserIdIn(ControllerWith().Me()).Should().BeNull();
    }

    [Fact]
    public void Me_Should_Issue_The_Antiforgery_Token()
    {
        // F1-T015/ADR-017: o XSRF-TOKEN nasce aqui porque o dashboard já chama
        // /auth/me no boot. Sem isso, a primeira escrita autenticada do dia
        // bateria em 400 — o token nunca existiu.
        var antiforgery = new Mock<IAntiforgery>();
        var controller = ControllerWithAntiforgery(
            antiforgery.Object, new Claim(ClaimTypes.NameIdentifier, "user-123"));

        controller.Me();

        antiforgery.Verify(antiforgery => antiforgery.GetAndStoreTokens(controller.HttpContext), Times.Once);
    }

    [Fact]
    public void Me_Should_Return_The_FullName_From_The_Claim()
    {
        // F38 — o nome completo viaja do cookie: /auth/me lê CLAIM, não o
        // banco. Sem a claim no CreatePrincipal, a UI nunca veria o valor.
        var controller = ControllerWith(
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim("displayName", "Ana"),
            new Claim("fullName", "Ana Maria da Silva"));

        FullNameIn(controller.Me()).Should().Be("Ana Maria da Silva");
    }

    [Fact]
    public void Me_Should_Return_Null_FullName_When_The_Claim_Is_Absent()
    {
        // Campo opcional: sessão antiga (emitida antes da claim existir) ou
        // usuário sem nome completo → null, não erro.
        var controller = ControllerWith(
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim("displayName", "Ana"));

        FullNameIn(controller.Me()).Should().BeNull();
    }
}
