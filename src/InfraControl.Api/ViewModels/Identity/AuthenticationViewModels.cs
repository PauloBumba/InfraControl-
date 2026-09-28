using System.ComponentModel.DataAnnotations;

namespace InfraControl.Api.ViewModels.Identity;

public sealed record RegisterViewModel(string Email, string DisplayName, string Password);
public sealed record LoginViewModel(string Email, string Password);
public sealed record RefreshSessionViewModel(string RefreshToken);
public sealed record ForgotPasswordViewModel(string Email);
public sealed record ResetPasswordViewModel(string Email, string Token, string NewPassword);
public sealed record ChangePasswordViewModel(string CurrentPassword, string NewPassword);

/// <summary>
/// F1-T014: apenas o id_token emitido pelo Google Identity Services no browser.
/// O Client Secret NUNCA trafega nem é conhecido pelo frontend — a validação
/// da assinatura acontece no backend. Limite de tamanho porque é entrada não
/// confiável (o envelope de erro continua sendo o ApiResponse pt-BR, F0-T023).
/// </summary>
public sealed record GoogleLoginViewModel(
    [property: StringLength(8192, MinimumLength = 1, ErrorMessage = "A credencial do Google é obrigatória.")]
    [property: Required(ErrorMessage = "A credencial do Google é obrigatória.")]
    string IdToken);
