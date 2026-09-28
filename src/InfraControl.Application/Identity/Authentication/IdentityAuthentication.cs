namespace InfraControl.Application.Identity.Authentication;

public sealed record RegisterUserRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshSessionRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email, string? BaseUrl = null);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ChangePasswordRequest(Guid UserId, string CurrentPassword, string NewPassword);
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

public interface IIdentityAuthenticationService
{
    Task<OperationResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationResult?> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default);
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);

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
}
