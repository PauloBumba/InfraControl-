namespace InfraControl.Application.Identity.Authentication;

public sealed record RegisterUserRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password, string? MfaCode = null);
public sealed record RefreshSessionRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email, string? BaseUrl = null);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ChangePasswordRequest(Guid UserId, string CurrentPassword, string NewPassword);
public sealed record UpdateProfileRequest(Guid UserId, string DisplayName);
public sealed record GoogleLoginRequest(string IdToken);

/// <summary>
/// Motivo da recusa do login social. O endpoint traduz TODOS os casos de
/// "conta" para a MESMA resposta (anti-enumeração, docs/05 F1-T013): e-mail
/// desconhecido e conta inativa produzem <see cref="AccountUnavailable"/>.
/// </summary>
public enum ExternalLoginFailure
{
    ProviderNotConfigured,
    InvalidCredential,
    AccountUnavailable
}

public sealed record ExternalLoginResult(AuthenticationResult? Result, ExternalLoginFailure? Failure)
{
    public bool Succeeded => Result is not null;

    public static ExternalLoginResult Success(AuthenticationResult result) => new(result, null);

    public static ExternalLoginResult Failed(ExternalLoginFailure failure) => new(null, failure);
}

public enum GoogleTokenStatus
{
    Valid,
    Invalid,
    NotConfigured
}

/// <summary>
/// Identidade extraída do id_token do Google APÓS validação de assinatura,
/// issuer, audiência, expiração e email_verified pela porta do adaptador.
/// </summary>
public sealed record GoogleIdentity(string Subject, string Email, string DisplayName);

public sealed record GoogleTokenValidation(GoogleTokenStatus Status, GoogleIdentity? Identity)
{
    public bool IsValid => Status == GoogleTokenStatus.Valid && Identity is not null;

    public static GoogleTokenValidation Valid(GoogleIdentity identity) => new(GoogleTokenStatus.Valid, identity);

    public static GoogleTokenValidation Invalid() => new(GoogleTokenStatus.Invalid, null);

    public static GoogleTokenValidation NotConfigured() => new(GoogleTokenStatus.NotConfigured, null);
}

/// <summary>
/// Porta de validação do id_token do Google (Google Identity Services). O
/// adaptador valida a assinatura contra as chaves públicas do Google — o
/// frontend NUNCA é considerado fonte de verdade (docs/05, docs/security).
/// </summary>
public interface IGoogleIdentityTokenValidator
{
    Task<GoogleTokenValidation> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record AuthenticatedUser(
    Guid UserId,
    Guid SessionId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles);

public sealed record AuthenticationResult(
    AuthenticatedUser User,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public sealed record PasswordResetRequestResult(string Message);
public sealed record OperationResult(bool Succeeded, IReadOnlyCollection<string> Errors)
{
    public static OperationResult Success() => new(true, []);
    public static OperationResult Failure(params string[] errors) => new(false, errors);
}

/// <summary>
/// Motivo da recusa do login por senha (F1-T016a, ADR-019). Senha errada e
/// conta inativa continuam <see cref="InvalidCredentials"/> (anti-enumeração,
/// mensagem única "Credenciais inválidas."); os outros dois motivos só
/// existem DEPOIS da senha validada e permitem que a UI mostre o campo de MFA.
/// </summary>
public enum LoginFailure
{
    InvalidCredentials,
    MfaRequired,
    MfaCodeInvalid
}

/// <summary>
/// Resultado do login por senha: sessão criada OU o motivo da recusa. Substitui
/// o antigo null mudo — o MFA exige mensagem própria na UI.
/// </summary>
public sealed record LoginOutcome(AuthenticationResult? Authentication, LoginFailure? Failure)
{
    public static LoginOutcome Success(AuthenticationResult authentication) => new(authentication, null);

    public static LoginOutcome Failed(LoginFailure failure) => new(null, failure);
}

/// <summary>Setup de MFA (F1-T016a): secret em Base32 p/ QR + URI otpauth.</summary>
public sealed record MfaSetupResult(string Secret, string OtpauthUri);

/// <summary>Confirm de MFA: em caso de sucesso entrega os 10 recovery codes (uma única vez).</summary>
public sealed record MfaConfirmResult(
    bool Succeeded,
    IReadOnlyCollection<string> RecoveryCodes,
    IReadOnlyCollection<string> Errors)
{
    public static MfaConfirmResult Success(IReadOnlyCollection<string> recoveryCodes) => new(true, recoveryCodes, []);

    public static MfaConfirmResult Fail(params string[] errors) => new(false, [], errors);
}

/// <summary>Status do MFA da conta autenticada (auto-serviço).</summary>
public sealed record MfaStatusResult(bool Configured, bool Enabled, DateTime? ConfirmedAt);

public interface IIdentityAuthenticationService
{
    Task<OperationResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationResult?> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default);
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// F29-T002 — auto-serviço: o próprio usuário edita o nome completo
    /// (<c>User.DisplayName</c>). Até aqui só o admin conseguia, via
    /// <c>PUT /admin/users/{id}</c>; o e-mail é preservado, então não há
    /// colisão de e-mail a verificar. Não revoga sessão (diferente da troca de
    /// senha): nada de segurança muda, só o nome exibido.
    /// </summary>
    Task<OperationResult> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// MFA TOTP (F1-T016a, ADR-019) — auto-serviço da conta autenticada.
    /// Setup emite secret novo (pendente, enabled=false); Confirm só liga o
    /// MFA com TOTP válido e devolve os 10 recovery codes uma vez; Disable
    /// exige código TOTP OU senha e apaga a linha; Status é leitura pura.
    /// </summary>
    Task<MfaSetupResult> SetupMfaAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MfaConfirmResult> ConfirmMfaAsync(Guid userId, string code, CancellationToken cancellationToken = default);
    Task<OperationResult> DisableMfaAsync(Guid userId, string? code, string? password, CancellationToken cancellationToken = default);
    Task<MfaStatusResult> GetMfaStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Login social (F1-T014). A credencial recebida do browser é validada
    /// server-side; o login só existe para conta JÁ CADASTRADA e ATIVA — nunca
    /// provisiona usuário (mesma regra de ativação da F1-T013).
    /// </summary>
    Task<ExternalLoginResult> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);
}

public interface IIdentityEmailSender
{
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default);

    /// <summary>F1-T017: convite por e-mail — link de aceite com o token de uso único.</summary>
    Task SendInvitationAsync(string email, string acceptUrl, CancellationToken cancellationToken = default);
}
