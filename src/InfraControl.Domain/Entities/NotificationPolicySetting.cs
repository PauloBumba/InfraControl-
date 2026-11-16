using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// ADR-070/F58: política de redução de ruído de notificações (linha
/// única — singleton, mesmo padrão de WhatsAppIntegrationSetting).
/// Nesta fase cobre a estabilização (flapping): minutos que um problema
/// precisa permanecer aberto antes da notificação de abertura sair —
/// ciclos que resolvem dentro da janela têm as entregas pendentes
/// canceladas e a resolução não notifica ("não notificar o ciclo").
/// 0 = desligado (notificação imediata, comportamento F55/F57).
/// Demais políticas do spec (lembretes, rate limit, manutenção,
/// agrupamento) chegam nas fases seguintes da F58 — colunas aditivas.
/// </summary>
public sealed class NotificationPolicySetting : Entity
{
    public const int MaxStabilizationMinutes = 120;

    /// <summary>Estabilização para problemas em nível de atenção (Warning).</summary>
    public int StabilizationWarningMinutes { get; private set; } = 5;

    /// <summary>
    /// Estabilização para problemas críticos — janela própria menor para
    /// não atrasar indiscriminadamente incidentes (spec §2).
    /// </summary>
    public int StabilizationCriticalMinutes { get; private set; } = 2;

    private NotificationPolicySetting() { }

    public static NotificationPolicySetting Create(
        int stabilizationWarningMinutes = 5,
        int stabilizationCriticalMinutes = 2)
    {
        var setting = new NotificationPolicySetting();
        setting.UpdateStabilization(
            stabilizationWarningMinutes, stabilizationCriticalMinutes);
        return setting;
    }

    public void UpdateStabilization(int warningMinutes, int criticalMinutes)
    {
        if (warningMinutes is < 0 or > MaxStabilizationMinutes)
            throw new ArgumentOutOfRangeException(
                nameof(warningMinutes),
                $"A estabilização de alerta deve ficar entre 0 e {MaxStabilizationMinutes} minutos.");
        if (criticalMinutes is < 0 or > MaxStabilizationMinutes)
            throw new ArgumentOutOfRangeException(
                nameof(criticalMinutes),
                $"A estabilização de incidente deve ficar entre 0 e {MaxStabilizationMinutes} minutos.");

        StabilizationWarningMinutes = warningMinutes;
        StabilizationCriticalMinutes = criticalMinutes;
    }
}
