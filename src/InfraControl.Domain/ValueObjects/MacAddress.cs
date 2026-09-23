using System.Text.RegularExpressions;

namespace InfraControl.Domain.ValueObjects;

public partial class MacAddress : ValueObject
{
    public string Value { get; }

    private MacAddress(string value)
    {
        Value = value;
    }

    public static MacAddress Create(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            throw new ArgumentException("O endereço MAC não pode ser vazio.", nameof(macAddress));

        var normalized = macAddress.Trim().ToUpperInvariant();

        if (!MacAddressRegex().IsMatch(normalized))
            throw new ArgumentException($"Formato de endereço MAC inválido: {macAddress}", nameof(macAddress));

        return new MacAddress(normalized);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^([0-9A-F]{2}[:-]){5}([0-9A-F]{2})$", RegexOptions.Compiled)]
    private static partial Regex MacAddressRegex();
}
