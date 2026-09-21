using System.Net;

namespace InfraControl.Domain.Common.ValueObjects;

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
            throw new ArgumentException("IP address cannot be empty.", nameof(ipAddress));

        if (!IPAddress.TryParse(ipAddress, out _))
            throw new ArgumentException($"Invalid IP address format: {ipAddress}", nameof(ipAddress));

        return new IpAddress(ipAddress);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
