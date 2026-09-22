using InfraControl.Application.Identity.Authentication;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthenticationController(IIdentityAuthenticationService authenticationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterAsync(request, cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created) : Conflict(result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request, cancellationToken);
        if (result is null) return Unauthorized(new { error = "Invalid credentials." });
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, CreatePrincipal(result.User));
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(request, cancellationToken);
        if (result is null) return Unauthorized(new { error = "Invalid or expired session." });
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, CreatePrincipal(result.User));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshSessionRequest? request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value, out var userId))
            return Unauthorized();

        await authenticationService.LogoutAsync(userId, request?.RefreshToken, cancellationToken);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await authenticationService.RequestPasswordResetAsync(request with { BaseUrl = baseUrl }, cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.ResetPasswordAsync(request, cancellationToken);
        return result.Succeeded ? NoContent() : BadRequest(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value, out var userId))
            return Unauthorized();

        var result = await authenticationService.ChangePasswordAsync(request with { UserId = userId }, cancellationToken);
        return result.Succeeded ? NoContent() : BadRequest(result);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
        => Ok(new
        {
            userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameidentifier")?.Value,
            email = User.Identity?.Name,
            roles = User.FindAll("role").Select(claim => claim.Value).ToArray(),
            permissions = User.FindAll("permission").Select(claim => claim.Value).ToArray()
        });

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
