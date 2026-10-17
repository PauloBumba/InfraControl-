namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Opções da API Yealink (F30-T006a — docs/47 §Credenciais): credencial
/// global por ambiente (config/env, espelha OnvifOptions/SnmpOptions —
/// credencial por device fica no F11 Credential Store, pendência da fase).
/// A senha nunca aparece em Values nem em mensagens de erro.
/// </summary>
public sealed class YealinkOptions
{
    public const string SectionName = "Monitoring:Yealink";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>Porta HTTP da API do telefone (Yealink padrão = 80).</summary>
    public int Port { get; set; } = 80;
}
