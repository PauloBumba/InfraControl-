using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F26 Fase 3.1: site do UniFi Controller persistido no inventário de
/// integração. `ShortId` é a identidade externa estável. Site que deixa de
/// aparecer no controller NÃO é apagado — ganha `MissingSinceUtc`
/// (regra: não destruir histórico por falha/remoção na origem).
/// </summary>
public sealed class UniFiSite : Entity
{
    public const int MaxLength = 128;

    public string ShortId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool Hidden { get; private set; }
    public DateTime? MissingSinceUtc { get; private set; }

    private UniFiSite() { }

    public static UniFiSite Create(string shortId, string name, string description, bool hidden)
    {
        if (string.IsNullOrWhiteSpace(shortId))
            throw new ArgumentException("O identificador do site é obrigatório.", nameof(shortId));

        return new UniFiSite
        {
            ShortId = shortId.Trim(),
            Name = Truncate(name, MaxLength),
            Description = Truncate(description, MaxLength * 4),
            Hidden = hidden
        };
    }

    public void UpdateFrom(string name, string description, bool hidden)
    {
        Name = Truncate(name, MaxLength);
        Description = Truncate(description, MaxLength * 4);
        Hidden = hidden;
        MissingSinceUtc = null;
        Touch();
    }

    public void MarkMissing()
    {
        MissingSinceUtc ??= AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        Touch();
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
