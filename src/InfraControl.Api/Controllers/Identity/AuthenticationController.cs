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
using System.Security.Claims;
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
    IOptions<FrontendOptions> frontendOptions) : BaseController
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
    // validada — a mensagem guia a UI ao campo do segundo fator.
    private static string LoginFailureMessage(LoginFailure? failure) => failure switch
    {
        LoginFailure.MfaRequired => "Código MFA obrigatório.",
        LoginFailure.MfaCodeInvalid => "Código MFA inválido.",
        _ => "Credenciais inválidas."
    };

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
                ApiResponse<object>.Fail(["Login com Google não está configurado."]));

        if (result.Failure == ExternalLoginFailure.InvalidCredential)
            return Unauthorized(ApiResponse<object>.Fail(["Credencial do Google inválida."]));

        if (result.Failure == ExternalLoginFailure.AccountUnavailable)
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail(["Conta não encontrada ou inativa. Fale com o administrador."]));

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
