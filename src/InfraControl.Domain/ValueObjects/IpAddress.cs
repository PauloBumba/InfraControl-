using System.Net;

namespace InfraControl.Domain.ValueObjects;

public class IpAddress : ValueObject
{
    public string Value { get; }

    private IpAddress(string value)
    {
        Value = value;
    }

    public static IpAddress Create(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            throw new ArgumentException("O endereço IP não pode ser vazio.", nameof(ipAddress));

        if (!IPAddress.TryParse(ipAddress, out _))
            throw new ArgumentException($"Formato de endereço IP inválido: {ipAddress}", nameof(ipAddress));

        return new IpAddress(ipAddress);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
