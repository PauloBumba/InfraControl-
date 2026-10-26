using InfraControl.Application.Identity.Authentication;
using InfraControl.Api.Controllers;
using InfraControl.Api.Middleware;
using InfraControl.Api.RateLimiting;
using InfraControl.Api.Security;
using InfraControl.Api.Services;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Auth;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InfraControl.Api.Controllers.Identity;

[Route("api/v1/auth")]
public sealed class AuthenticationController(
    IIdentityAuthenticationService authenticationService,
    IAntiforgery antiforgery,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<GoogleAuthOptions> googleAuthOptions) : BaseController
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterAsync(new RegisterUserRequest(model.Email, model.DisplayName, model.Password), cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok())
            : Conflict(ApiResponse<object>.Fail(result.Errors));
    }

    [AllowAnonymous]
    [HttpRateLimit(RateLimitCategory.Login)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var outcome = await authenticationService.LoginAsync(
            new LoginRequest(model.Email, model.Password, model.MfaCode), cancellationToken);

        if (outcome.Authentication is null)
            return Unauthorized(ApiResponse<object>.Fail([LoginFailureMessage(outcome.Failure)]));

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, CreatePrincipal(outcome.Authentication.User));
        return Ok(ApiResponse.Ok(outcome.Authentication));
    }

    // F1-T016a (ADR-019): os motivos de MFA só existem depois da senha
    // validada — a mensagem guia a UI ao segundo fator.
    private static string LoginFailureMessage(LoginFailure? failure) => failure switch
    {
        LoginFailure.MfaRequired => "Código MFA obrigatório.",
        LoginFailure.MfaCodeInvalid => "Código MFA inválido.",
        _ => "Credenciais inválidas."
    };

    // F43: textos do login social — fonte única do POST e do endpoint de
    // mensagem por código (o callback só transporta `reason=account`).
    private const string GoogleUnavailableMessage = "Login com Google não está configurado.";
    private const string GoogleInvalidCredentialMessage = "Credencial do Google inválida.";
    private const string GoogleAccountUnavailableMessage =
        "Conta não encontrada ou inativa. Fale com o administrador.";
    private const string GoogleGenericLoginErrorMessage =
        "Não foi possível entrar com Google. Tente novamente ou use e-mail e senha.";

    /// <summary>
    /// F43-T001/ADR-064: start do authorization code flow — emite o
    /// <c>state</c> CSRF em cookie (HttpOnly, SameSite=Lax, 10 min) e
    /// redireciona para o consent screen do Google. A troca
    /// <c>code → token</c> e a validação da conta acontecem inteiramente no
    /// <c>GET google/callback</c> (T002), no servidor. Sem config completa
    /// (id+secret+redirect) responde 503 — mesmo contrato do POST google.
    /// </summary>
    [AllowAnonymous]
    [HttpRateLimit(RateLimitCategory.Login)]
    [HttpGet("google")]
    public IActionResult GoogleLoginStart()
    {
        var options = googleAuthOptions.Value;
        if (!options.IsCodeFlowConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail([GoogleUnavailableMessage]));

        var state = GoogleOAuthFlow.CreateState();
        Response.Cookies.Append(
            GoogleOAuthFlow.StateCookieName,
            state,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = GoogleOAuthFlow.StateLifetime
            });

        return Redirect(GoogleOAuthFlow.CreateAuthorizationUrl(options, state));
    }

    /// <summary>
    /// F43-T002/ADR-064: callback do authorization code — o servidor confere
    /// o <c>state</c> (cookie vs query, comparação em tempo fixo e de um só
    /// uso), troca o <c>code</c> pelo <c>id_token</c> no servidor e reusa
    /// <c>LoginWithGoogleAsync</c> (validação JWKS + conta ativa +
    /// anti-enumeração, F1-T013). Falhas operacionais (state/troca/provider/
    /// credencial) viram o redirect genérico <c>/login?error=google</c>; a
    /// conta indisponível acrescenta <c>&amp;reason=account</c> para a UI
    /// buscar a mensagem no backend (rev. 2026-10-24, ver
    /// <see cref="GoogleLoginFailure"/>). O code/token jamais aparecem em URL
    /// ou log.
    /// </summary>
    [AllowAnonymous]
    [HttpRateLimit(RateLimitCategory.Login)]
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleLoginCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromServices] IGoogleTokenClient tokenClient,
        CancellationToken cancellationToken)
    {
        var loginError = Redirect(FrontendUrl("/login") + "?error=google");

        // state de um só uso: lido e invalidado ANTES de qualquer
        // processamento — replay do callback recaiu num cookie já morto.
        Request.Cookies.TryGetValue(GoogleOAuthFlow.StateCookieName, out var cookieState);
        if (!string.IsNullOrEmpty(cookieState))
        {
            Response.Cookies.Delete(GoogleOAuthFlow.StateCookieName, new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
        }

        var options = googleAuthOptions.Value;
        if (!options.IsCodeFlowConfigured)
            return loginError;

        if (string.IsNullOrEmpty(cookieState) || string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
            return loginError;

        // Tempo fixo: state não é segredo, mas a comparação não pode vazar
        // o prefixo por timing (RFC 6749 §10.12).
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cookieState), Encoding.UTF8.GetBytes(state)))
            return loginError;

        var idToken = await tokenClient.ExchangeCodeForIdTokenAsync(code, cancellationToken);
        if (string.IsNullOrEmpty(idToken))
            return loginError;

        var result = await authenticationService.LoginWithGoogleAsync(
            new GoogleLoginRequest(idToken), cancellationToken);

        if (result.Failure is not null || result.Result is null)
            return Redirect(GoogleLoginErrorRedirect(result.Failure));

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme, CreatePrincipal(result.Result.User));
        return Redirect(FrontendUrl("/"));
    }

    /// <summary>
    /// F43-T003 (rev. 2026-10-24): falha do login social. Apenas o motivo
    /// "conta indisponível" ganha código na URL — o usuário já provou aquele
    /// e-mail no Google, então esconder que a conta não está ativa só o
    /// deixava sem ação. A mensagem continua sendo do backend (a UI busca em
    /// <see cref="GoogleLoginFailure"/>) e é única para conta inexistente e
    /// inativa (anti-enumeração preservada). Os demais motivos seguem
    /// indistinguíveis.
    /// </summary>
    private string GoogleLoginErrorRedirect(ExternalLoginFailure? failure)
    {
        var url = FrontendUrl("/login") + "?error=google";
        return failure == ExternalLoginFailure.AccountUnavailable
            ? url + "&reason=" + GoogleOAuthFlow.AccountUnavailableReason
            : url;
    }

    /// <summary>
    /// F43-T003 (rev.): mensagem do erro do login social por código — fonte
    /// única do texto no backend (a UI não mantém catálogo). Código
    /// desconhecido ou ausente = mensagem genérica. Endpoint público barato,
    /// sem estado: nunca devolve se o e-mail existe.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("google/failure")]
    public IActionResult GoogleLoginFailure([FromQuery] string? reason)
    {
        var known = reason == GoogleOAuthFlow.AccountUnavailableReason;
        return Ok(ApiResponse.Ok(new GoogleLoginFailureViewModel(
            known ? GoogleOAuthFlow.AccountUnavailableReason : "unknown",
            known ? GoogleAccountUnavailableMessage : GoogleGenericLoginErrorMessage)));
    }

    /// <summary>URL absoluta do SPA — mesmo fallback do link de reset (F1-T017).</summary>
    private string FrontendUrl(string path)
    {
        var configured = frontendOptions.Value.BaseUrl;
        var baseUrl = string.IsNullOrWhiteSpace(configured)
            ? $"{Request.Scheme}://{Request.Host}"
            : configured.TrimEnd('/');
        return baseUrl + path;
    }

    [AllowAnonymous]
    [HttpRateLimit(RateLimitCategory.Login)]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshSessionViewModel model, CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(new RefreshSessionRequest(model.RefreshToken), cancellationToken);
        if (result is null) return Unauthorized(ApiResponse<object>.Fail(["Sessão inválida ou expirada."]));
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, CreatePrincipal(result.User));
        return Ok(ApiResponse.Ok(result));
    }

    [AllowAnonymous]
    [HttpRateLimit(RateLimitCategory.Login)]
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginViewModel model, CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginWithGoogleAsync(
            new GoogleLoginRequest(model.IdToken), cancellationToken);

        // Mensagens fixas por motivo: e-mail desconhecido e conta inativa
        // recebem a MESMA resposta (anti-enumeração, docs/05 F1-T013). O
        // id_token nunca é ecoado na resposta nem registrado em log.
        if (result.Failure == ExternalLoginFailure.ProviderNotConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail([GoogleUnavailableMessage]));

        if (result.Failure == ExternalLoginFailure.InvalidCredential)
            return Unauthorized(ApiResponse<object>.Fail([GoogleInvalidCredentialMessage]));

        if (result.Failure == ExternalLoginFailure.AccountUnavailable)
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail([GoogleAccountUnavailableMessage]));

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, CreatePrincipal(result.Result!.User));
        return Ok(ApiResponse.Ok(result.Result));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshSessionViewModel? model, CancellationToken cancellationToken)
    {
        if (User.GetSessionUserIdOrNull() is not { } userId)
            return Unauthorized(ApiResponse<object>.Fail(["É necessário um id de usuário autenticado."]));

        await authenticationService.LogoutAsync(userId, model?.RefreshToken, cancellationToken);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpRateLimit(RateLimitCategory.PasswordReset)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        // Origem do link do e-mail: config Frontend:BaseUrl (decisão do usuário
        // de 01/10/2026, MESMA do convite F1-T017) com fallback à origem da
        // própria request (produção com nginx same-origin serve SPA e API no
        // mesmo host). O link aponta para a página pública /reset-password do
        // SPA — não para a API: a rota GET não existe (só POST → 405) e o fluxo
        // precisa de tela (pendência fechada com a página ResetPasswordPage).
        var configured = frontendOptions.Value.BaseUrl;
        var baseUrl = string.IsNullOrWhiteSpace(configured)
            ? $"{Request.Scheme}://{Request.Host}"
            : configured.TrimEnd('/');
        var result = await authenticationService.RequestPasswordResetAsync(new ForgotPasswordRequest(model.Email, baseUrl), cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [AllowAnonymous]
    [HttpRateLimit(RateLimitCategory.PasswordReset)]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        var result = await authenticationService.ResetPasswordAsync(new ResetPasswordRequest(model.Email, model.Token, model.NewPassword), cancellationToken);
        return result.Succeeded ? NoContent() : BadRequest(ApiResponse<object>.Fail(result.Errors));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (User.GetSessionUserIdOrNull() is not { } userId)
            return Unauthorized(ApiResponse<object>.Fail(["É necessário um id de usuário autenticado."]));

        var result = await authenticationService.ChangePasswordAsync(new ChangePasswordRequest(userId, model.CurrentPassword, model.NewPassword), cancellationToken);
        return result.Succeeded ? NoContent() : BadRequest(ApiResponse<object>.Fail(result.Errors));
    }

    /// <summary>
    /// F29-T002 — perfil de autoedição: o próprio usuário edita o nome
    /// completo (<c>User.DisplayName</c>), que até aqui só o admin conseguia
    /// alterar em <c>PUT /admin/users/{id}</c>. O e-mail não muda aqui.
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model, CancellationToken cancellationToken)
    {
        if (User.GetSessionUserIdOrNull() is not { } userId)
            return Unauthorized(ApiResponse<object>.Fail(["É necessário um id de usuário autenticado."]));

        var result = await authenticationService.UpdateProfileAsync(
            new UpdateProfileRequest(userId, model.DisplayName, model.FullName), cancellationToken);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<object>.Fail(result.Errors));

        // O nome exibido vem da CLAIM do cookie (o /auth/me lê a claim, não o
        // banco). Sem re-emitir aqui, o usuário salvaria e veria o nome antigo
        // no reload seguinte. Re-emitimos preservando as claims atuais — em
        // especial SessionIdClaim, que o ActiveSessionMiddleware confere:
        // criar uma sessão nova derrubaria a atual.
        //
        // FullName acompanha a MESMA regra do banco: ausente no corpo = não
        // informado (mantém a claim atual), "" = limpa (claim some).
        var identity = (ClaimsIdentity)User.Identity!;
        var fullName = model.FullName switch
        {
            null => identity.FindFirst("fullName")?.Value,
            "" => null,
            _ => model.FullName.Trim()
        };

        var claims = identity.Claims
            .Where(claim => claim.Type != "displayName" && claim.Type != "fullName")
            .Append(new Claim("displayName", model.DisplayName.Trim()))
            .ToList();

        if (!string.IsNullOrWhiteSpace(fullName))
            claims.Add(new Claim("fullName", fullName));

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(
                claims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType)));

        return Ok(ApiResponse.Ok(new { displayName = model.DisplayName.Trim(), fullName }));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        // F1-T015/ADR-017: nasce aqui o XSRF-TOKEN. O /auth/me já é chamado no
        // boot do dashboard (SessionGate), então o token existe antes da
        // primeira escrita e não custa endpoint nem round trip extra.
        // Precisa ser autenticado porque o token é vinculado à identidade.
        // EmitForJavaScript (e não GetAndStoreTokens) porque o JS precisa do
        // request-token: publicar só o cookie-token faria o header sair errado
        // e toda escrita autenticada cair em 400.
        AntiforgeryCookieSetup.EmitForJavaScript(HttpContext, antiforgery);

        return Ok(ApiResponse.Ok(new
        {
            userId = User.GetSessionUserId(),
            email = User.Identity?.Name,
            displayName = User.FindFirst("displayName")?.Value,
            // F38 — nome completo (coluna nova, nullable). Claim emitida no
            // CreatePrincipal e re-emisso no PUT /auth/profile.
            fullName = User.FindFirst("fullName")?.Value,
            roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray()
        }));
    }

    private static ClaimsPrincipal CreatePrincipal(AuthenticatedUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email),

            // ADR-018: o cookie diz QUAL sessão é. Sem esta claim o
            // ActiveSessionMiddleware não tem o que conferir e rejeita
            // todo mundo — inclusive os cookies novos.
            new(ActiveSessionMiddleware.SessionIdClaim, user.SessionId.ToString())
        };

        // Nome de exibição para a UI (claim própria: ClaimTypes.Name continua
        // sendo o e-mail, usado por ICurrentUserContext/auditoria).
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            claims.Add(new Claim("displayName", user.DisplayName.Trim()));

        // F38 — nome completo (nullable): claim só quando preenchido; o
        // /auth/me lê daqui, então ela nasce junto com a sessão.
        if (!string.IsNullOrWhiteSpace(user.FullName))
            claims.Add(new Claim("fullName", user.FullName.Trim()));

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
    }
}
