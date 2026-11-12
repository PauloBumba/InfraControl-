namespace InfraControl.Domain.Enums;

/// <summary>
/// Parse do código de baixa (F52-T000) — mesmo formato do
/// <c>ChecklistOwnerTypes</c>: valor do enum ignoreCase com 400 pt-BR
/// apontando os valores válidos. Números no wire são rejeitados
/// (ADR-061: contrato é string nomeada).
/// </summary>
public static class ResolutionCodes
{
    public static ResolutionCode Parse(string? code)
    {
        var trimmed = code?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed)
            && Enum.TryParse<ResolutionCode>(trimmed, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            && Enum.GetNames<ResolutionCode>()
                .Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return parsed;
        }

        throw new ArgumentException(
            $"Código de baixa inválido: '{code}'. " +
            "Use 'Fixed', 'Workaround', 'Duplicate', 'NoAction' ou 'Other'.",
            nameof(code));
    }
}
