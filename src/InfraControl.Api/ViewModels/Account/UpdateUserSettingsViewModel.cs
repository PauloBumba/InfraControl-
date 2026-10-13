namespace InfraControl.Api.ViewModels.Account;

/// <summary>
/// Payload de PUT /api/v1/account/settings (Fase 06d). O UserId NÃO vem
/// aqui — é derivado da sessão no controller.
/// </summary>
public sealed record UpdateUserSettingsViewModel(
    string Timezone,
    bool DarkMode,
    bool AutoRefresh,
    bool Telemetry);
