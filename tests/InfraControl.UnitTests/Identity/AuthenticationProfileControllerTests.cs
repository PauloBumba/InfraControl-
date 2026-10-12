using FluentAssertions;
using InfraControl.Api.Controllers.Identity;
using InfraControl.Api.Middleware;
using InfraControl.Api.Services;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.Application.Identity.Authentication;
using InfraControl.BuildingBlocks.Application.Response;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F29-T002 — perfil de autoedição (PUT /auth/profile).
///
/// O <c>User.DisplayName</c> (o nome completo) já existia, mas só o admin
/// conseguia editá-lo, via <c>PUT /admin/users/{id}</c>. Este endpoint abre a
/// edição para o próprio usuário — o serviço reutiliza <c>User.Update</c>
/// como está, nenhum código de domínio novo.
///
/// O que estes testes tranca não é o UPDATE (isso já funcionava no caminho do
/// admin): é que o nome exibido vem da CLAIM do cookie, então sem re-emitir o
/// cookie o usuário salvaria e veria o nome antigo no reload seguinte. Por
/// isso o ponto central é a re-emição — e o fato de ela preservar o
/// <c>SessionIdClaim</c>: o <c>ActiveSessionMiddleware</c> confere essa claim,
/// então uma sessão nova derrubaria a sessão do usuário.
/// </summary>
public sealed class AuthenticationProfileControllerTests
{
    private sealed record Harness(
        AuthenticationController Controller,
        Mock<IIdentityAuthenticationService> Auth,
        Mock<IAuthenticationService> SignIn);

    private static Harness Build(Guid? sessionUserId, params Claim[] extraClaims)
    {
        var claims = extraClaims.ToList();
        if (sessionUserId is { } id)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        var signIn = new Mock<IAuthenticationService>();
        signIn
            .Setup(service => service.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var services = new Mock<IServiceProvider>();
        services
            .Setup(provider => provider.GetService(typeof(IAuthenticationService)))
            .Returns(signIn.Object);

        var auth = new Mock<IIdentityAuthenticationService>();

        var controller = new AuthenticationController(
            auth.Object,
            Mock.Of<IAntiforgery>(),
            Options.Create(new FrontendOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme")),
                    RequestServices = services.Object
                }
            }
        };

        return new Harness(controller, auth, signIn);
    }

    private static ClaimsPrincipal? ReissuedPrincipal(Mock<IAuthenticationService> signIn)
    {
        var call = signIn.Invocations
            .FirstOrDefault(invocation => invocation.Method.Name == nameof(IAuthenticationService.SignInAsync));
        return call is null ? null : (ClaimsPrincipal)call.Arguments[2];
    }

    private static Harness WithSuccessfulUpdate(Guid userId, Claim[]? claims = null)
    {
        var harness = Build(userId, claims ?? []);
        harness.Auth
            .Setup(service => service.UpdateProfileAsync(
                It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Success());
        return harness;
    }

    [Fact]
    public async Task UpdateProfile_Should_Send_The_Own_UserId_And_The_New_Name()
    {
        var userId = Guid.NewGuid();
        var harness = WithSuccessfulUpdate(
            userId,
            [new Claim(ClaimTypes.Email, "ana@exemplo.com")]);

        var result = await harness.Controller.UpdateProfile(
            new UpdateProfileViewModel("Ana Completa"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        harness.Auth.Verify(service => service.UpdateProfileAsync(
            It.Is<UpdateProfileRequest>(request => request.UserId == userId && request.DisplayName == "Ana Completa"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_Should_Reissue_The_Cookie_With_The_New_Name()
    {
        var harness = WithSuccessfulUpdate(
            Guid.NewGuid(),
            [new Claim("displayName", "Ana Antiga")]);

        await harness.Controller.UpdateProfile(
            new UpdateProfileViewModel("Ana Completa"), CancellationToken.None);

        var reissued = ReissuedPrincipal(harness.SignIn);
        reissued.Should().NotBeNull("sem re-emição o nome volta ao antigo no próximo reload");
        reissued!.FindFirst("displayName")?.Value.Should().Be("Ana Completa");
    }

    [Fact]
    public async Task UpdateProfile_Should_Preserve_The_Session_And_The_Roles()
    {
        var sessionId = Guid.NewGuid().ToString();
        var harness = WithSuccessfulUpdate(
            Guid.NewGuid(),
            [
                new Claim(ClaimTypes.Email, "ana@exemplo.com"),
                new Claim("displayName", "Ana Antiga"),
                new Claim(ClaimTypes.Role, "Viewer"),
                new Claim(ClaimTypes.Role, "Operator"),
                new Claim(ActiveSessionMiddleware.SessionIdClaim, sessionId)
            ]);

        await harness.Controller.UpdateProfile(
            new UpdateProfileViewModel("Ana Completa"), CancellationToken.None);

        var identity = (ClaimsIdentity)ReissuedPrincipal(harness.SignIn)!.Identities.First();

        // Uma sessão nova derrubaria a atual no ActiveSessionMiddleware.
        identity.FindFirst(ActiveSessionMiddleware.SessionIdClaim)?.Value.Should().Be(sessionId);
        identity.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
            .Should().BeEquivalentTo(new[] { "Viewer", "Operator" });
        identity.FindFirst(ClaimTypes.Email)?.Value.Should().Be("ana@exemplo.com");
    }

    [Fact]
    public async Task UpdateProfile_Should_Not_Reissue_When_The_Service_Rejects()
    {
        var harness = Build(Guid.NewGuid());
        harness.Auth
            .Setup(service => service.UpdateProfileAsync(
                It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Failure("O nome de exibição é obrigatório."));

        var result = await harness.Controller.UpdateProfile(
            new UpdateProfileViewModel("   "), CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ApiResponse<object>>()
            .Which.Errors.Should().Contain("O nome de exibição é obrigatório.");

        harness.SignIn.Verify(service => service.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_Should_Follow_The_Same_Failure_Contract_As_ChangePassword()
    {
        // O contrato do serviço é OperationResult (igual ao ChangePassword):
        // usuário sumiu = Failure, não 404. Mesmo mapeamento nos dois.
        var harness = Build(Guid.NewGuid());
        harness.Auth
            .Setup(service => service.UpdateProfileAsync(
                It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Failure("Usuário não encontrado."));

        var result = await harness.Controller.UpdateProfile(
            new UpdateProfileViewModel("Ana Completa"), CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ApiResponse<object>>()
            .Which.Errors.Should().Contain("Usuário não encontrado.");
        harness.SignIn.Verify(service => service.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_Should_Return_Unauthorized_Without_A_Session_Identity()
    {
        var harness = Build(null);

        var result = await harness.Controller.UpdateProfile(
            new UpdateProfileViewModel("Ana Completa"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        harness.Auth.Verify(service => service.UpdateProfileAsync(
            It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
