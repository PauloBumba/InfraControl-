using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Sessions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpNet;

namespace InfraControl.Infrastructure.Auth;

public sealed class IdentityAuthenticationService(
    UserManager<InfraControlUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AppDbContext dbContext,
    ICacheService cache,
    IIdentityEmailSender emailSender,
    IGoogleIdentityTokenValidator googleTokenValidator,
    ICredentialProtector credentialProtector,
    IOptions<MfaOptions> mfaOptions,
    IOptions<SessionLifetimeOptions> sessionOptions,
    IAuditWriter auditWriter,
    ILogger<IdentityAuthenticationService> logger) : IIdentityAuthenticationService
{
    // ADR-018/ADR-020: os prazos vêm da configuração (antes: constantes
    // estáticas que ignoravam o appsettings que a ADR-018 dizia existir) e o
    // teto é cortado por role no login — cap só encurta, o menor valor vence.
    private TimeSpan SlidingLifetime => sessionOptions.Value.SlidingLifetime;

    private TimeSpan AbsoluteLifetimeFor(User domainUser)
    {
        var absolute = sessionOptions.Value.AbsoluteLifetime;
        foreach (var role in domainUser.Roles)
        {
            if (sessionOptions.Value.TryGetRoleCap(role.ToString(), out var cap) && cap < absolute)
                absolute = cap;
        }

        return absolute;
    }

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

    public async Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var identityUser = await userManager.FindByEmailAsync(email);
        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, request.Password))
            return LoginOutcome.Failed(LoginFailure.InvalidCredentials);

        // Roles moram na coleção _roleAssignments (campo mapeado): sem o
        // Include o aggregate volta sem elas, o cookie nasce sem ClaimTypes.Role
        // e toda policy nega. Mesmo Include de IdentityAdministrationService.
        var domainUser = await dbContext.DomainUsers
            .Include(user => user.RoleAssignments)
            .FirstOrDefaultAsync(user => user.Id == identityUser.Id, cancellationToken);
        if (domainUser is null || !domainUser.IsActive)
            return LoginOutcome.Failed(LoginFailure.InvalidCredentials);

        // ADR-019: a senha é validada PRIMEIRO; só depois o MFA (se ativo)
        // destrava a sessão. Falha de MFA nunca cria sessão.
        var mfa = await dbContext.UserMfas.FirstOrDefaultAsync(item => item.UserId == domainUser.Id, cancellationToken);
        if (mfa is { Enabled: true })
        {
            if (string.IsNullOrWhiteSpace(request.MfaCode))
                return LoginOutcome.Failed(LoginFailure.MfaRequired);

            if (!VerifyMfaCode(mfa, request.MfaCode))
                return LoginOutcome.Failed(LoginFailure.MfaCodeInvalid);

            // Persiste o consumo de recovery code (quando foi o caso) antes
            // de criar a sessão — uso único precisa ser registrado.
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // F1-T016c: só o login que passou pelo 2º fator vira evento; a linha
        // entra na mesma transação da sessão (StartSessionAsync salva em seguida).
        if (mfa is { Enabled: true })
            await SecurityAudit.WriteAsync(
                auditWriter, "LoginWithMfa", "Session", identityUser.UserName, cancellationToken);

        return LoginOutcome.Success(await StartSessionAsync(identityUser, domainUser, cancellationToken));
    }

    public async Task<AuthenticationResult?> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken = default)
    {
        var hash = Hash(request.RefreshToken);
        var session = await dbContext.UserSessions.FirstOrDefaultAsync(item => item.RefreshTokenHash == hash, cancellationToken);
        if (session is null || !session.IsActive)
            return null;

        var identityUser = await userManager.FindByIdAsync(session.UserId.ToString());
        var domainUser = await dbContext.DomainUsers
            .Include(user => user.RoleAssignments)
            .FirstOrDefaultAsync(user => user.Id == session.UserId, cancellationToken);
        if (identityUser is null || domainUser is null || !domainUser.IsActive)
            return null;

        var refreshToken = GenerateToken();
        session.Refresh(Hash(refreshToken), SlidingLifetime);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(identityUser, domainUser, session, refreshToken, session.ExpiresAt);
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.UserSessions
            .Where(session => session.UserId == userId && (refreshToken == null || session.RefreshTokenHash == Hash(refreshToken)))
            .ToListAsync(cancellationToken);
        foreach (var session in sessions) session.Revoke();
        // F1-T016c: logout derruba sessões — audit antes do SaveChanges (mesma transação).
        await SecurityAudit.WriteAsync(auditWriter, dbContext, "Logout", "Session", userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // ADR-020: revogar é duas escritas — banco e cache compartilhado.
        await InvalidateSessionCacheAsync(sessions.Select(session => session.Id), cancellationToken);
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

            // O link e-maiado nasce na PÁGINA pública /reset-password do SPA
            // (a rota GET da API não existe: só POST → 405; o fluxo morria no
            // e-mail). Origem vem do controller (Frontend:BaseUrl com fallback
            // à request — decisão 01/10/2026); sem origem o link sai relativo,
            // como o do aceite de convite.
            var resetUrl = $"{baseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
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

    /// <summary>
    /// F29-T002 — auto-serviço do perfil. Reutiliza o MESMO caminho do admin
    /// (<c>UpdateUserAsync</c>): o e-mail volta igual, então <c>User.Update</c>
    /// só aplica o nome e nem dispara <c>UserUpdatedDomainEvent</c> quando o
    /// valor não mudou (early return no domínio).
    /// Não chama <c>RevokeAllSessionsAsync</c> de propósito: trocar o nome não
    /// é evento de segurança — diferente da senha, ninguém perde a sessão.
    /// </summary>
    public async Task<OperationResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var domainUser = await dbContext.DomainUsers
            .FirstOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);
        if (domainUser is null)
            return OperationResult.Failure("Usuário não encontrado.");

        try
        {
            domainUser.Update(request.DisplayName, domainUser.Email);
        }
        catch (ArgumentException exception)
        {
            // RequireDisplayName lança pt-BR; converte para o contrato
            // OperationResult, igual ao ChangePasswordAsync faz com o Identity.
            return OperationResult.Failure(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }

    // ── MFA TOTP (F1-T016a, ADR-019) ───────────────────────────────────

    public async Task<MfaSetupResult> SetupMfaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var mfa = await dbContext.UserMfas.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        // Reemitir secret de uma conta já habilitada sem desativar antes
        // derrubaria a 2ª camada sem verificação.
        if (mfa is { Enabled: true })
            throw new InvalidOperationException("MFA já está habilitado. Desative antes de reconfigurar.");

        var domainUser = await dbContext.DomainUsers.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        if (mfa is null)
        {
            mfa = UserMfa.Create(userId, credentialProtector.Protect(secret));
            dbContext.UserMfas.Add(mfa);
        }
        else
        {
            mfa.ResetSecret(credentialProtector.Protect(secret));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("MFA setup started for user {UserId}.", userId);

        var otpauthUri = new OtpUri(OtpType.Totp, secret, domainUser.Email, "InfraControl").ToString();
        return new MfaSetupResult(secret, otpauthUri);
    }

    public async Task<MfaConfirmResult> ConfirmMfaAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var mfa = await dbContext.UserMfas.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (mfa is null || mfa.Enabled)
            return MfaConfirmResult.Fail("MFA não está configurado para este usuário.");

        if (!VerifyTotp(mfa, code))
            return MfaConfirmResult.Fail("Código MFA inválido.");

        // Entregues UMA vez; no DB fica só o hash SHA-256 de cada um.
        var recoveryCodes = GenerateRecoveryCodes();
        mfa.Enable(recoveryCodes.Select(item => Hash(NormalizeRecoveryCode(item))).ToArray());
        // F1-T016c: MFA ativado é evento de segurança — audit na mesma transação.
        await SecurityAudit.WriteAsync(auditWriter, dbContext, "MfaEnabled", "Mfa", userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("MFA enabled for user {UserId}.", userId);
        return MfaConfirmResult.Success(recoveryCodes);
    }

    public async Task<OperationResult> DisableMfaAsync(Guid userId, string? code, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(password))
            return OperationResult.Failure("Informe o código MFA ou a senha.");

        var mfa = await dbContext.UserMfas.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (mfa is null)
            return OperationResult.Failure("MFA não está configurado para este usuário.");

        var verified = false;
        if (!string.IsNullOrWhiteSpace(password))
        {
            var identityUser = await userManager.FindByIdAsync(userId.ToString());
            verified = identityUser is not null && await userManager.CheckPasswordAsync(identityUser, password);
        }

        if (!verified && !string.IsNullOrWhiteSpace(code))
            verified = VerifyMfaCode(mfa, code);

        if (!verified)
            return OperationResult.Failure("Código ou senha inválidos.");

        dbContext.UserMfas.Remove(mfa);
        // F1-T016c: MFA desativado é evento de segurança — audit na mesma transação.
        await SecurityAudit.WriteAsync(auditWriter, dbContext, "MfaDisabled", "Mfa", userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("MFA disabled for user {UserId}.", userId);
        return OperationResult.Success();
    }

    public async Task<MfaStatusResult> GetMfaStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var mfa = await dbContext.UserMfas.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return mfa is null
            ? new MfaStatusResult(false, false, null)
            : new MfaStatusResult(true, mfa.Enabled, mfa.ConfirmedAt);
    }

    /// <summary>
    /// Confere um código contra o MFA do usuário: 6 dígitos → TOTP (janela
    /// curta, ADR-019); qualquer outro formato normalizado → recovery code de
    /// uso único (marca UsedAt, que é persistido pelo chamador).
    /// </summary>
    private bool VerifyMfaCode(UserMfa mfa, string code)
        => IsTotpCode(code) ? VerifyTotp(mfa, code) : mfa.UseRecoveryCode(Hash(NormalizeRecoveryCode(code)));

    private bool VerifyTotp(UserMfa mfa, string code)
    {
        var secret = credentialProtector.Unprotect(mfa.SecretEncrypted);
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var tolerance = Math.Max(1, mfaOptions.Value.ToleranceSteps);
        var window = new VerificationWindow(previous: tolerance, future: tolerance);
        return totp.VerifyTotp(NormalizeMfaCode(code), out _, window);
    }

    private static bool IsTotpCode(string? code)
    {
        var normalized = NormalizeMfaCode(code);
        return normalized.Length == 6 && normalized.All(char.IsDigit);
    }

    private static string NormalizeMfaCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? string.Empty : code.Replace(" ", string.Empty).Trim();

    private static string NormalizeRecoveryCode(string code)
        => code.Replace("-", string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();

    private static string[] GenerateRecoveryCodes()
    {
        // Base32 (RFC 4648): 8 caracteres no formato XXXX-XXXX — mesmo
        // alfabeto do TOTP, sem caracteres de confusão (0/O, 1/I/L).
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var codes = new string[10];
        for (var i = 0; i < codes.Length; i++)
        {
            var chars = new char[8];
            for (var j = 0; j < chars.Length; j++)
                chars[j] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            codes[i] = string.Concat(chars.AsSpan(0, 4), "-", chars.AsSpan(4, 4));
        }
        return codes;
    }

    /// <summary>
    /// F1-T014 — login social Google (Google Identity Services). A credencial
    /// chega do browser, mas quem decide é o adaptador: assinatura, audiência,
    /// emissor, expiração e e-mail verificado. Depois disso, o login só existe
    /// para conta JÁ CADASTRADA e ATIVA (mesma regra da F1-T013) — nada é
    /// provisionado e nada de vínculo externo é criado automaticamente.
    /// E-mail desconhecido e conta inativa retornam a MESMA falha
    /// (AccountUnavailable) para não enumerar contas.
    /// </summary>
    public async Task<ExternalLoginResult> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);

        if (validation.Status == GoogleTokenStatus.NotConfigured)
            return ExternalLoginResult.Failed(ExternalLoginFailure.ProviderNotConfigured);

        if (!validation.IsValid)
        {
            logger.LogWarning("Google login denied: the credential was not validated by the token validator.");
            return ExternalLoginResult.Failed(ExternalLoginFailure.InvalidCredential);
        }

        var email = NormalizeEmail(validation.Identity!.Email);
        var identityUser = await userManager.FindByEmailAsync(email);
        var domainUser = identityUser is null
            ? null
            : await dbContext.DomainUsers
                .Include(user => user.RoleAssignments)
                .FirstOrDefaultAsync(user => user.Id == identityUser.Id, cancellationToken);

        // Sem provisionamento e sem autoativação: conta ausente ou inativa é a
        // mesma resposta. O e-mail NÃO é logado (LGPD, docs/security).
        if (identityUser is null || domainUser is null || !domainUser.IsActive)
        {
            logger.LogWarning("Google login denied: no active account matches the validated e-mail.");
            return ExternalLoginResult.Failed(ExternalLoginFailure.AccountUnavailable);
        }

        var result = await StartSessionAsync(identityUser, domainUser, cancellationToken);
        logger.LogInformation("User {UserId} signed in with Google.", identityUser.Id);
        return ExternalLoginResult.Success(result);
    }

    private async Task<AuthenticationResult> StartSessionAsync(InfraControlUser identityUser, User domainUser, CancellationToken cancellationToken)
    {
        var refreshToken = GenerateToken();
        var session = UserSession.Start(
            domainUser.Id, Hash(refreshToken), SlidingLifetime, AbsoluteLifetimeFor(domainUser));
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(identityUser, domainUser, session, refreshToken, session.ExpiresAt);
    }

    private async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions.Where(session => session.UserId == userId && session.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var session in sessions) session.Revoke();
        // F1-T016c: troca/reset de senha derruba as sessões — audit do evento.
        await SecurityAudit.WriteAsync(auditWriter, dbContext, "SessionsRevoked", "Session", userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // ADR-020: trocar/resetar a senha derruba os cookies das outras
        // instâncias na hora, não no próximo CheckInterval.
        await InvalidateSessionCacheAsync(sessions.Select(session => session.Id), cancellationToken);
    }

    /// <summary>
    /// ADR-020: a segunda escrita da revogação. Gravar o check "Revoked" (e
    /// não apagar a chave) faz a outra instância ler o negativo na hora —
    /// apagar deixaria o fallback local dela servindo o positivo velho.
    /// </summary>
    private async Task InvalidateSessionCacheAsync(IEnumerable<Guid> sessionIds, CancellationToken cancellationToken)
    {
        var revoked = new ActiveSessionCheck(ActiveSessionStatus.Revoked);
        foreach (var sessionId in sessionIds)
        {
            await cache.SetAsync(
                ActiveSessionCache.Key(sessionId), revoked, sessionOptions.Value.CheckInterval, cancellationToken);
        }
    }

    private async Task EnsureRoleAsync(string role, CancellationToken cancellationToken)
    {
        if (!await roleManager.RoleExistsAsync(role))
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)), "criação de função");
    }

    private static AuthenticationResult ToResult(InfraControlUser identityUser, User domainUser, UserSession session, string refreshToken, DateTime expiresAt)
        => new(
            new AuthenticatedUser(
                identityUser.Id,
                session.Id,
                identityUser.Email!,
                domainUser.DisplayName,
                domainUser.Roles.Select(role => role.ToString()).ToArray()),
            refreshToken,
            expiresAt);

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

    public Task SendInvitationAsync(string email, string acceptUrl, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Invitation link generated for {Email}: {AcceptUrl}", email, acceptUrl);
        return Task.CompletedTask;
    }
}
