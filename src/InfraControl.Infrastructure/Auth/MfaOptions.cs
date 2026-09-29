namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// Política de tolerância do TOTP (F1-T016a, ADR-019): janela "curta" =
/// ±1 período de 30s por padrão, ajustável em `Mfa:ToleranceSteps` sem
/// redeploy. Tolerância maior abriria janela de replay/reuso de código.
/// </summary>
public sealed class MfaOptions
{
    public const string SectionName = "Mfa";

    public int ToleranceSteps { get; set; } = 1;
}
