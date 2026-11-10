using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F39-T003 (ADR-063): política de roteamento de notificações — defaults
/// globais da instalação. Linha única (singleton por instalação).
/// <see cref="Enabled"/> = false ou ausência de linha → o fanout mantém o
/// comportamento legado (compatibilidade, docs/62 §2.5). Cada gravação
/// incrementa <see cref="Version"/>; a versão efetiva é registrada junto
/// das novas deliveries em F39-T004.
/// Canais = nomes do enum <c>NotificationChannel</c> separados por vírgula;
/// lista vazia = nenhum canal (sem fallback silencioso para Email).
/// </summary>
public sealed class NotificationRoutingPolicy : Entity
{
    public const int MaxDefaultsLength = 500;

    public bool Enabled { get; private set; }
    public string Defaults { get; private set; } = string.Empty;
    public int Version { get; private set; } = 1;

    private NotificationRoutingPolicy() { }

    public static NotificationRoutingPolicy Create(bool enabled, string defaults)
    {
        var policy = new NotificationRoutingPolicy();
        policy.Update(enabled, defaults);
        policy.Version = 1;
        return policy;
    }

    public void Update(bool enabled, string defaults)
    {
        if (defaults is { Length: > MaxDefaultsLength })
            throw new ArgumentException(
                $"A lista de canais padrão pode ter no máximo {MaxDefaultsLength} caracteres.",
                nameof(defaults));

        Enabled = enabled;
        Defaults = defaults?.Trim() ?? string.Empty;
        if (Version > 0)
            Version++;
        Touch();
    }

    /// <summary>Canais padrão parseados (nomes do enum), na ordem configurada.</summary>
    public IReadOnlyList<string> DefaultChannelNames =>
        string.IsNullOrWhiteSpace(Defaults)
            ? Array.Empty<string>()
            : Defaults.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
