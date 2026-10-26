using FluentAssertions;
using InfraControl.Api.Controllers.Identity;
using InfraControl.Api.Services;
using InfraControl.Application.Identity.Authentication;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Infrastructure.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F43-T001 — authorization code flow com callback no backend (ADR-064).
/// O start (GET /auth/google) emite o state CSRF em cookie SameSite=Lax
/// (o redirect de volta do Google é cross-site; Strict não enviado) e
/// redireciona para o consent screen; sem config completa responde 503
/// (mesmo contrato do POST /auth/google antigo).
/// </summary>
public sealed class GoogleCodeFlowTests
{
    private static AuthenticationController CreateController(
        GoogleAuthOptions? options = null)
    {
        var controller = new AuthenticationController(
            Mock.Of<IIdentityAuthenticationService>(),
            Mock.Of<IAntiforgery>(),
            Options.Create(new FrontendOptions()),
            Options.Create(options ?? new GoogleAuthOptions()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    [Fact]
    public void GoogleAuthOptions_CodeFlow_ShouldRequireClientIdSecretAndRedirectUri()
    {
        var onlyId = new GoogleAuthOptions { ClientId = "id" };
        onlyId.IsConfigured.Should().BeTrue();
        onlyId.IsCodeFlowConfigured.Should().BeFalse();

        var full = new GoogleAuthOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            RedirectUri = "https://host/api/v1/auth/google/callback"
        };
        full.IsCodeFlowConfigured.Should().BeTrue();
    }

    [Fact]
    public void AuthorizationUrl_ShouldCarryCodeFlowParameters()
    {
        var options = new GoogleAuthOptions
        {
            ClientId = "client.apps.googleusercontent.com",
            ClientSecret = "server-only",
            RedirectUri = "https://localhost:8445/api/v1/auth/google/callback"
        };

        var url = GoogleOAuthFlow.CreateAuthorizationUrl(options, "state-abc");

        url.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth?");
        url.Should().Contain("client_id=client.apps.googleusercontent.com");
        url.Should().Contain("response_type=code");
        url.Should().Contain("state=state-abc");
        url.Should().Contain("scope=openid%20email%20profile");
        // redirect_uri percent-encoded (contém : e /)
        url.Should().Contain(
            "redirect_uri=https%3A%2F%2Flocalhost%3A8445%2Fapi%2Fv1%2Fauth%2Fgoogle%2Fcallback");
        // O secret NUNCA aparece na URL enviada ao navegador.
        url.Should().NotContain("server-only");
    }

    [Fact]
    public void CreateState_ShouldBeUniqueUrlSafeAndLongEnough()
    {
        var first = GoogleOAuthFlow.CreateState();
        var second = GoogleOAuthFlow.CreateState();

        first.Should().NotBe(second);
        first.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        first.Length.Should().BeGreaterThanOrEqualTo(43); // 32 bytes em base64url
    }

    [Fact]
    public void GoogleStart_WithoutConfig_ShouldReturn503()
    {
        var controller = CreateController(new GoogleAuthOptions { ClientId = "so-id" });

        var result = controller.GoogleLoginStart();

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        var payload = status.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        payload.IsSuccess.Should().BeFalse();
        payload.Errors.Should().ContainSingle()
            .Which.Should().Contain("não está configurado");
    }

    [Fact]
    public void GoogleStart_WithConfig_ShouldRedirectToGoogleWithStateCookie()
    {
        var controller = CreateController(new GoogleAuthOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            RedirectUri = "https://localhost:8445/api/v1/auth/google/callback"
        });

        var result = controller.GoogleLoginStart();

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth?");

        // DefaultHttpContext serializa o cookie direto no header Set-Cookie
        // (ASP.NET emite os atributos em minúsculas — RFC 6265 case-insensitive).
        var setCookie = controller.Response.Headers["Set-Cookie"].ToString();
        var setCookieLower = setCookie.ToLowerInvariant();
        setCookie.Should().Contain(GoogleOAuthFlow.StateCookieName);
        setCookieLower.Should().Contain("samesite=lax");
        setCookieLower.Should().Contain("secure", "o cookie precisa ser Secure (HTTPS)");
        setCookieLower.Should().Contain("httponly");

        // O state da URL é o MESMO emitido no cookie (round-trip CSRF).
        var stateFromUrl = Uri.UnescapeDataString(
            redirect.Url.Split("state=")[1].Split('&')[0]);
        setCookie.Should().Contain(stateFromUrl);
    }

    // ── F43-T002 — callback (troca code → token + login) ─────────────────

    private const string FrontendBase = "https://localhost:8445";

    private static (AuthenticationController Controller, Mock<IGoogleTokenClient> TokenClient,
        Mock<IIdentityAuthenticationService> Service) CreateCallbackController(
        GoogleAuthOptions? options = null,
        string? cookieState = "state-abc",
        string? queryState = "state-abc",
        string? code = "code-123")
    {
        var authServices = new ServiceCollection()
            .AddLogging()
            .AddAuthentication(opt => opt.DefaultScheme = IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme)
            .Services
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = authServices };

        var queryParts = new List<string>();
        if (code is not null)
            queryParts.Add("code=" + Uri.EscapeDataString(code));
        if (queryState is not null)
            queryParts.Add("state=" + Uri.EscapeDataString(queryState));
        httpContext.Request.QueryString = queryParts.Count > 0
            ? new QueryString("?" + string.Join("&", queryParts))
            : QueryString.Empty;

        if (cookieState is not null)
            httpContext.Request.Headers.Cookie =
                $"{GoogleOAuthFlow.StateCookieName}={cookieState}";

        var tokenClient = new Mock<IGoogleTokenClient>();
        tokenClient
            .Setup(item => item.ExchangeCodeForIdTokenAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("id.token.troca");

        var service = new Mock<IIdentityAuthenticationService>();

        var controller = new AuthenticationController(
            service.Object,
            Mock.Of<IAntiforgery>(),
            Options.Create(new FrontendOptions { BaseUrl = FrontendBase }),
            Options.Create(options ?? CodeFlowOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        return (controller, tokenClient, service);
    }

    private static GoogleAuthOptions CodeFlowOptions() => new()
    {
        ClientId = "id",
        ClientSecret = "secret",
        RedirectUri = "https://localhost:8445/api/v1/auth/google/callback"
    };

    private static string LoginErrorUrl => $"{FrontendBase}/login?error=google";

    [Fact]
    public async Task Callback_StateMismatch_ShouldRedirectToLoginError_WithoutTokenExchange()
    {
        var (controller, tokenClient, service) = CreateCallbackController(
            cookieState: "state-abc", queryState: "state-outro");

        var result = await controller.GoogleLoginCallback(
            "code-123", "state-outro", tokenClient.Object, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(LoginErrorUrl,
            "a divergência de state é login CSRF e não pode chegar perto do token");
        tokenClient.Verify(item => item.ExchangeCodeForIdTokenAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(item => item.LoginWithGoogleAsync(
            It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Callback_WithoutStateCookie_ShouldRedirectToLoginError()
    {
        var (controller, tokenClient, _) = CreateCallbackController(cookieState: null);

        var result = await controller.GoogleLoginCallback(
            "code-123", "state-abc", tokenClient.Object, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be(LoginErrorUrl);
        tokenClient.Verify(item => item.ExchangeCodeForIdTokenAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Callback_WithoutCode_ShouldRedirectToLoginError()
    {
        // Usuário negou o consentimento: o Google volta sem `code`
        // (error=access_denied). Não há o que trocar.
        var (controller, tokenClient, service) = CreateCallbackController(code: null);

        var result = await controller.GoogleLoginCallback(
            null, "state-abc", tokenClient.Object, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be(LoginErrorUrl);
        tokenClient.Verify(item => item.ExchangeCodeForIdTokenAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(item => item.LoginWithGoogleAsync(
            It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Callback_WithoutConfig_ShouldRedirectToLoginError()
    {
        var (controller, tokenClient, _) = CreateCallbackController(
            options: new GoogleAuthOptions { ClientId = "so-id" });

        var result = await controller.GoogleLoginCallback(
            "code-123", "state-abc", tokenClient.Object, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be(LoginErrorUrl);
        tokenClient.Verify(item => item.ExchangeCodeForIdTokenAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Callback_TokenExchangeFails_ShouldRedirectToLoginError()
    {
        var (controller, tokenClient, service) = CreateCallbackController();
        tokenClient
            .Setup(item => item.ExchangeCodeForIdTokenAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await controller.GoogleLoginCallback(
            "code-123", "state-abc", tokenClient.Object, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be(LoginErrorUrl,
            "sem id_token não há sessão — falha da troca é indistinguível");
        service.Verify(item => item.LoginWithGoogleAsync(
            It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ExternalLoginFailure.InvalidCredential)]
    [InlineData(ExternalLoginFailure.ProviderNotConfigured)]
    public async Task Callback_LoginFailures_ShouldAllRedirectToLoginError(
        ExternalLoginFailure failure)
    {
        // Anti-enumeração (F1-T013): credencial inválida e provider sem
        // config caem no MESMO redirect — o browser não aprende qual motivo.
        // "Conta indisponível" é a exceção documentada (rev. 2026-10-24):
        // leva `reason=account` para a UI buscar a mensagem no backend.
        var (controller, tokenClient, service) = CreateCallbackController();
        service
            .Setup(item => item.LoginWithGoogleAsync(
                It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExternalLoginResult.Failed(failure));

        var result = await controller.GoogleLoginCallback(
            "code-123", "state-abc", tokenClient.Object, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be(LoginErrorUrl);
        service.Verify(item => item.LoginWithGoogleAsync(
            It.Is<GoogleLoginRequest>(request => request.IdToken == "id.token.troca"),
            It.IsAny<CancellationToken>()), Times.Once,
            "o id_token trocado no servidor é o que vai para a porta de validação");
    }

    [Fact]
    public async Task Callback_AccountUnavailable_ShouldRedirectWithReasonCode()
    {
        // A mensagem (conta inexistente OU inativa, sem distinguir) vem do
        // backend; na URL vai só o código do motivo — nunca texto livre.
        var (controller, tokenClient, service) = CreateCallbackController();
        service
            .Setup(item => item.LoginWithGoogleAsync(
                It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExternalLoginResult.Failed(ExternalLoginFailure.AccountUnavailable));

        var result = await controller.GoogleLoginCallback(
            "code-123", "state-abc", tokenClient.Object, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Which.Url
            .Should().Be($"{LoginErrorUrl}&reason={GoogleOAuthFlow.AccountUnavailableReason}");
    }

    [Fact]
    public void GoogleLoginFailure_AccountUnavailable_ShouldReturnBackendMessage()
    {
        var (controller, _, _) = CreateCallbackController();

        var result = controller.GoogleLoginFailure(GoogleOAuthFlow.AccountUnavailableReason);

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<InfraControl.Api.ViewModels.Identity.GoogleLoginFailureViewModel>>()
            .Subject;
        response.Data.Should().NotBeNull();
        var payload = response.Data!;
        payload.Code.Should().Be(GoogleOAuthFlow.AccountUnavailableReason);
        payload.Message.Should().Be(
            "Conta não encontrada ou inativa. Fale com o administrador.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qualquer-coisa")]
    public void GoogleLoginFailure_UnknownReason_ShouldReturnGenericMessage(string? reason)
    {
        var (controller, _, _) = CreateCallbackController();

        var result = controller.GoogleLoginFailure(reason);

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<InfraControl.Api.ViewModels.Identity.GoogleLoginFailureViewModel>>()
            .Subject;
        response.Data.Should().NotBeNull();
        var payload = response.Data!;
        payload.Code.Should().Be("unknown");
        payload.Message.Should().Be(
            "Não foi possível entrar com Google. Tente novamente ou use e-mail e senha.");
    }

    [Fact]
    public async Task Callback_Success_ShouldSignInSessionAndRedirectToFrontend()
    {
        var (controller, tokenClient, service) = CreateCallbackController();
        var session = new AuthenticationResult(
            new AuthenticatedUser(
                Guid.NewGuid(), Guid.NewGuid(), "user@exemplo.test",
                "User", ["Operator"]),
            "refresh-token", DateTime.UtcNow.AddMinutes(30));
        service
            .Setup(item => item.LoginWithGoogleAsync(
                It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExternalLoginResult.Success(session));

        var result = await controller.GoogleLoginCallback(
            "code-123", "state-abc", tokenClient.Object, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be($"{FrontendBase}/");
        tokenClient.Verify(item => item.ExchangeCodeForIdTokenAsync(
            "code-123", It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(item => item.LoginWithGoogleAsync(
            It.Is<GoogleLoginRequest>(request => request.IdToken == "id.token.troca"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Sessão emitida (cookie Identity) + state invalidado (one-time):
        var setCookie = controller.Response.Headers["Set-Cookie"].ToString();
        setCookie.Should().Contain(GoogleOAuthFlow.StateCookieName,
            "o state é de um só uso e precisa morrer no callback");
        setCookie.Should().Contain("Identity", "a sessão Application é emitida aqui");
    }
}
