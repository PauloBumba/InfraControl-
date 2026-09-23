using System.Security.Cryptography;
using System.Text;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.Auth;

public sealed class IdentityAuthenticationService(
    UserManager<InfraControlUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AppDbContext dbContext,
    ICacheService cache,
    IIdentityEmailSender emailSender,
    ILogger<IdentityAuthenticationService> logger) : IIdentityAuthenticationService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public async Task<OperationResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await dbContext.DomainUsers.AnyAsync(user => user.Email == email, cancellationToken))
            return OperationResult.Failure("Já existe um usuário com este e-mail.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var identityUser = new InfraControlUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        var createResult = await userManager.CreateAsync(identityUser, request.Password);
        if (!createResult.Succeeded)
            return OperationResult.Failure(createResult.Errors.Select(error => error.Description).ToArray());

        var domainUser = User.CreatePending(identityUser.Id, email, request.DisplayName, [UserRole.Viewer]);
        dbContext.DomainUsers.Add(domainUser);
        await EnsureRoleAsync(UserRole.Viewer.ToString(), cancellationToken);
        EnsureSucceeded(await userManager.AddToRoleAsync(identityUser, UserRole.Viewer.ToString()), "atribuição de função");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("User {UserId} registered pending activation.", identityUser.Id);
        return OperationResult.Success();
    }

    public async Task<AuthenticationResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var identityUser = await userManager.FindByEmailAsync(email);
        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, request.Password))
            return null;

        var domainUser = await dbContext.DomainUsers.FirstOrDefaultAsync(user => user.Id == identityUser.Id, cancellationToken);
        if (domainUser is null || !domainUser.IsActive)
            return null;

        return await StartSessionAsync(identityUser, domainUser, cancellationToken);
    }

    public async Task<AuthenticationResult?> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken = default)
    {
        var hash = Hash(request.RefreshToken);
        var session = await dbContext.UserSessions.FirstOrDefaultAsync(item => item.RefreshTokenHash == hash, cancellationToken);
        if (session is null || !session.IsActive)
            return null;

        var identityUser = await userManager.FindByIdAsync(session.UserId.ToString());
        var domainUser = await dbContext.DomainUsers.FirstOrDefaultAsync(user => user.Id == session.UserId, cancellationToken);
        if (identityUser is null || domainUser is null || !domainUser.IsActive)
            return null;

        var refreshToken = GenerateToken();
        session.Refresh(Hash(refreshToken), SessionLifetime);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(identityUser, domainUser, refreshToken, session.ExpiresAt);
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.UserSessions
            .Where(session => session.UserId == userId && (refreshToken == null || session.RefreshTokenHash == Hash(refreshToken)))
            .ToListAsync(cancellationToken);
        foreach (var session in sessions) session.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var identityUser = await userManager.FindByEmailAsync(email);

        // Guard: recuperação de senha só para contas ATIVAS. A resposta é
        // idêntica para email desconhecido/inativo (anti-enumeração).
        var domainUser = identityUser is null
            ? null
            : await dbContext.DomainUsers.FirstOrDefaultAsync(user => user.Id == identityUser.Id, cancellationToken);

        if (identityUser is not null && domainUser is not null && domainUser.IsActive)
        {
            var token = GenerateToken();
            await cache.SetAsync($"identity:password-reset:{Hash(token)}", email, TimeSpan.FromMinutes(30), cancellationToken);
            var baseUrl = request.BaseUrl ?? string.Empty;
            var resetUrl = $"{baseUrl.TrimEnd('/')}/api/v1/auth/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            await emailSender.SendPasswordResetAsync(email, resetUrl, cancellationToken);
        }

        return new PasswordResetRequestResult("Se o e-mail estiver cadastrado, as instruções de redefinição de senha serão enviadas.");
    }

    public async Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var identityUser = await userManager.FindByEmailAsync(NormalizeEmail(request.Email));
        if (identityUser is null)
            return OperationResult.Failure("Solicitação de redefinição de senha inválida.");

        // Guard: conta inativa/desabilitada não pode redefinir senha, mesmo
        // com token válido emitido antes da desativação.
        var domainUser = await dbContext.DomainUsers.FirstOrDefaultAsync(user => user.Id == identityUser.Id, cancellationToken);
        if (domainUser is null || !domainUser.IsActive)
            return OperationResult.Failure("Solicitação de redefinição de senha inválida.");

        var tokenKey = $"identity:password-reset:{Hash(request.Token)}";
        var tokenEmail = await cache.GetAsync<string>(tokenKey, cancellationToken);
        if (!string.Equals(tokenEmail, identityUser.Email, StringComparison.OrdinalIgnoreCase))
            return OperationResult.Failure("Token de redefinição de senha inválido ou expirado.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var removeResult = await userManager.RemovePasswordAsync(identityUser);
        if (!removeResult.Succeeded)
            return OperationResult.Failure(removeResult.Errors.Select(error => error.Description).ToArray());

        var result = await userManager.AddPasswordAsync(identityUser, request.NewPassword);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Failure(result.Errors.Select(error => error.Description).ToArray());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await cache.RemoveAsync(tokenKey, cancellationToken);
        await RevokeAllSessionsAsync(identityUser.Id, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var identityUser = await userManager.FindByIdAsync(request.UserId.ToString());
        if (identityUser is null)
            return OperationResult.Failure("Usuário não encontrado.");

        var result = await userManager.ChangePasswordAsync(identityUser, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return OperationResult.Failure(result.Errors.Select(error => error.Description).ToArray());

        await RevokeAllSessionsAsync(request.UserId, cancellationToken);
        return OperationResult.Success();
    }

    private async Task<AuthenticationResult> StartSessionAsync(InfraControlUser identityUser, User domainUser, CancellationToken cancellationToken)
    {
        var refreshToken = GenerateToken();
        var session = UserSession.Start(domainUser.Id, Hash(refreshToken), SessionLifetime);
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(identityUser, domainUser, refreshToken, session.ExpiresAt);
    }

    private async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions.Where(session => session.UserId == userId && session.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var session in sessions) session.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRoleAsync(string role, CancellationToken cancellationToken)
    {
        if (!await roleManager.RoleExistsAsync(role))
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)), "criação de função");
    }

    private static AuthenticationResult ToResult(InfraControlUser identityUser, User domainUser, string refreshToken, DateTime expiresAt)
        => new(new AuthenticatedUser(identityUser.Id, identityUser.Email!, domainUser.DisplayName, domainUser.Roles.Select(role => role.ToString()).ToArray()), refreshToken, expiresAt);

    private static string NormalizeEmail(string email) => string.IsNullOrWhiteSpace(email) ? throw new ArgumentException("O e-mail é obrigatório.", nameof(email)) : email.Trim().ToLowerInvariant();
    private static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded) throw new InvalidOperationException($"Não foi possível concluir {operation}: {string.Join(", ", result.Errors.Select(error => error.Description))}");
    }
}

public sealed class LoggingIdentityEmailSender(ILogger<LoggingIdentityEmailSender> logger) : IIdentityEmailSender
{
    public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Password reset link generated for {Email}: {ResetUrl}", email, resetUrl);
        return Task.CompletedTask;
    }
}
