using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Preferências do usuário autenticado (Fase 06d): uma por usuário.
/// Guarda só o que é preferência PESSOAL de UI/comportamento — configurações
/// de controller (UniFi/rede) e políticas de segurança não moram aqui.
/// </summary>
public sealed class UserSettings : Entity
{
    public const int TimezoneMaxLength = 64;

    public Guid UserId { get; private set; }
    public string Timezone { get; private set; } = "America/Sao_Paulo";
    public bool DarkMode { get; private set; } = true;
    public bool AutoRefresh { get; private set; } = true;
    public bool Telemetry { get; private set; }

    private UserSettings() { }

    public static UserSettings CreateDefault(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));

        return new UserSettings { UserId = userId };
    }

    public void Update(
        string timezone,
        bool darkMode,
        bool autoRefresh,
        bool telemetry)
    {
        Timezone = RequireTimezone(timezone);
        DarkMode = darkMode;
        AutoRefresh = autoRefresh;
        Telemetry = telemetry;
        Touch();
    }

    private static string RequireTimezone(string timezone)
    {
        var trimmed = timezone?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new ArgumentException("O fuso horário é obrigatório.", nameof(timezone));
        if (trimmed.Length > TimezoneMaxLength)
            throw new ArgumentException(
                $"O fuso horário deve ter no máximo {TimezoneMaxLength} caracteres.",
                nameof(timezone));
        return trimmed;
    }
}
