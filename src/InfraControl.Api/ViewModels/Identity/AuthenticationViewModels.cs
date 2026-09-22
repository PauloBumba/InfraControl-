namespace InfraControl.Api.ViewModels.Identity;

public sealed record RegisterViewModel(string Email, string DisplayName, string Password);
public sealed record LoginViewModel(string Email, string Password);
public sealed record RefreshSessionViewModel(string RefreshToken);
public sealed record ForgotPasswordViewModel(string Email);
public sealed record ResetPasswordViewModel(string Email, string Token, string NewPassword);
public sealed record ChangePasswordViewModel(string CurrentPassword, string NewPassword);
