using InfraControl.Application.Identity.Authentication;
using InfraControl.Api.Controllers;
using InfraControl.Api.RateLimiting;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Identity;

[Route("api/v1/auth")]
public sealed class AuthenticationController(IIdentityAuthenticationService authenticationService) : BaseController
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
        var result = await authenticationService.LoginAsync(new LoginRequest(model.Email, model.Password), cancellationToken);
        if (result is null) return Unauthorized(ApiResponse<object>.Fail(["Credenciais inválidas."]));
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, CreatePrincipal(result.User));
        return Ok(ApiResponse.Ok(result));
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

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshSessionViewModel? model, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value, out var userId))
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
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
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
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value, out var userId))
            return Unauthorized(ApiResponse<object>.Fail(["É necessário um id de usuário autenticado."]));

        var result = await authenticationService.ChangePasswordAsync(new ChangePasswordRequest(userId, model.CurrentPassword, model.NewPassword), cancellationToken);
        return result.Succeeded ? NoContent() : BadRequest(ApiResponse<object>.Fail(result.Errors));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
        => Ok(ApiResponse.Ok(new
        {
            userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value,
            email = User.Identity?.Name,
            roles = User.FindAll("role").Select(claim => claim.Value).ToArray(),
            permissions = User.FindAll("permission").Select(claim => claim.Value).ToArray()
        }));

    private static ClaimsPrincipal CreatePrincipal(AuthenticatedUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email)
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            if (Enum.TryParse<UserRole>(role, out var parsedRole))
            {
                claims.AddRange(RolePermissions.ForRole(parsedRole)
                    .Select(permission => new Claim(PermissionClaim.Type, permission.ToString())));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
    }
}
