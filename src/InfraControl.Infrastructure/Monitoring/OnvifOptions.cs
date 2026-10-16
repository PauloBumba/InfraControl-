namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Opções ONVIF (F30-T005a — docs/46 §Credenciais): credencial global por
/// ambiente (config/env, espelha SnmpOptions — credencial por device fica
/// no F11 Credential Store, pendência da fase). A senha nunca aparece em
/// Values nem em mensagens de erro.
/// </summary>
public sealed class OnvifOptions
{
    public const string SectionName = "Monitoring:Onvif";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>Porta HTTP da câmera/NVR (ONVIF padrão = 80).</summary>
    public int Port { get; set; } = 80;
}
