namespace InfraControl.Application.Account.Settings;

/// <summary>
/// Preferências do usuário autenticado (Fase 06d).
/// </summary>
public sealed record UserSettingsDto(
    Guid UserId,
    string Timezone,
    bool DarkMode,
    bool AutoRefresh,
    bool Telemetry);
