using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// MAC cadastrado em uma interface — NetworkInterface 1:N MacAddress (F2-T005),
/// MAC único GLOBAL (case-insensitive: pré-check no handler + índice LOWER(mac)
/// na migração). Normalização aa:bb:cc:dd:ee:ff em pt-BR; aceita separadores
/// :, -, . ou 12 hex colados.
/// Observação: existe um ValueObject órfão em Domain/ValueObjects/MacAddress.cs
/// (sem consumidores, normalização UPPER) — fora do escopo desta task.
/// </summary>
public sealed class MacAddress : AggregateRoot
{
    public const int MacMaxLength = 17;

    public Guid InterfaceId { get; private set; }
    public string Mac { get; private set; } = string.Empty;

    private MacAddress() { }

    public static MacAddress Create(Guid interfaceId, string mac)
    {
        if (interfaceId == Guid.Empty)
            throw new ArgumentException("O id da interface é obrigatório.", nameof(interfaceId));

        var entity = new MacAddress();
        entity.InterfaceId = interfaceId;
        entity.Update(mac);
        return entity;
    }

    public void Update(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac))
            throw new ArgumentException("O MAC é obrigatório.", nameof(mac));

        mac = mac.Trim();
        if (mac.Length > MacMaxLength)
            throw new ArgumentException(
                $"O MAC pode ter no máximo {MacMaxLength} caracteres.", nameof(mac));

        Mac = Normalize(mac)
            ?? throw new ArgumentException("O MAC informado é inválido.", nameof(mac));
    }

    private static string? Normalize(string value)
    {
        var compact = value
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .ToLowerInvariant();

        if (compact.Length != 12 || compact.Any(c => !Uri.IsHexDigit(c)))
            return null;

        return string.Join(':', Enumerable.Range(0, 6).Select(i => compact.Substring(i * 2, 2)));
    }
}
