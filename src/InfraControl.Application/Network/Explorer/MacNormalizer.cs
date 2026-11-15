namespace InfraControl.Application.Network.Explorer;

/// <summary>
/// Normalização de MAC para o Network Explorer: remove separadores
/// (:, -, ., espaço) e devolve 12 hex minúsculos — null se não for MAC válido.
/// Compartilhado pelo MAC Lookup e pelo Switch Explorer.
/// </summary>
internal static class MacNormalizer
{
    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var compact = value
            .Where(c => c is not (':' or '-' or '.' or ' '))
            .ToArray();

        if (compact.Length != 12 || compact.Any(c => !Uri.IsHexDigit(c)))
            return null;

        return new string(compact).ToLowerInvariant();
    }
}
