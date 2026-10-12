using System.ComponentModel.DataAnnotations;

namespace InfraControl.Api.ViewModels.Identity;

public sealed record RegisterViewModel(string Email, string DisplayName, string Password);

/// <summary>
/// F1-T016a: MfaCode é opcional — só quem tem MFA habilitado envia no
/// segundo passo; o backend decide o que é obrigatório (ADR-019).
/// </summary>
public sealed record LoginViewModel(string Email, string Password, string? MfaCode = null);
public sealed record RefreshSessionViewModel(string RefreshToken);
public sealed record ForgotPasswordViewModel(string Email);
public sealed record ResetPasswordViewModel(string Email, string Token, string NewPassword);
public sealed record ChangePasswordViewModel(string CurrentPassword, string NewPassword);

/// <summary>
/// F29-T002 — perfil de autoedição. Só o nome: o e-mail é somente leitura
/// aqui (trocar e-mail é operação de admin, com verificação de unicidade em
/// <c>UpdateUserAsync</c>), então não entra no contrato deste endpoint.
/// </summary>
public sealed record UpdateProfileViewModel(string DisplayName);

/// <summary>Confirm do MFA (F1-T016a): um código TOTP de 6 dígitos.</summary>
public sealed record MfaConfirmViewModel(string Code);

/// <summary>Disable do MFA (F1-T016a): código TOTP OU senha (pelo menos um).</summary>
public sealed record MfaDisableViewModel(string? Code, string? Password);

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
