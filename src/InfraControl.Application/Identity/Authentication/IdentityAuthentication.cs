namespace InfraControl.Application.Identity.Authentication;

public sealed record RegisterUserRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshSessionRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email, string? BaseUrl = null);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ChangePasswordRequest(Guid UserId, string CurrentPassword, string NewPassword);

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
}

public interface IIdentityEmailSender
{
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default);
}
