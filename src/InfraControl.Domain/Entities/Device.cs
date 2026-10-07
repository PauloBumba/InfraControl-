using System.Net;
using System.Net.Sockets;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Profiles;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Equipamento heterogêneo do inventário (F2-T003) — Location 1:N Device,
/// Rack 1:N Device. Nome único global (case-insensitive: pré-check no handler +
/// índice LOWER(name) na migração). Mensagens de validação em pt-BR.
/// DeviceProfile (F2-T008 / ADR-014): checks por tipo materializados no create,
/// com override opcional (<see cref="ProfileIsCustom"/>).
/// </summary>
public sealed class Device : AggregateRoot
{
    public const int NameMaxLength = 120;
    public const int IpMaxLength = 45;
    public const int MacMaxLength = 17;
    public const int SerialMaxLength = 100;
    public const int ModelMaxLength = 100;
    public const int VendorMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int MaxMonitoringChecks = 20;

    public Guid LocationId { get; private set; }
    public Guid? RackId { get; private set; }
    public DeviceType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Ip { get; private set; }
    public string? Mac { get; private set; }
    public string? Serial { get; private set; }
    public string? Model { get; private set; }
    public string? Vendor { get; private set; }
    public Guid? VlanId { get; private set; }
    public string? Description { get; private set; }

    /// <summary>Checks aplicados a este device (default do tipo ou override).</summary>
    public IReadOnlyList<CheckKind> MonitoringChecks => _monitoringChecks.AsReadOnly();

    /// <summary>True quando o operador sobrescreveu o default do tipo (ADR-014).</summary>
    public bool ProfileIsCustom { get; private set; }

    private List<CheckKind> _monitoringChecks = [];

    private Device() { }

    public static Device Create(
        Guid locationId,
        string name,
        DeviceType type,
        Guid? rackId = null,
        string? ip = null,
        string? mac = null,
        string? serial = null,
        string? model = null,
        string? vendor = null,
        Guid? vlanId = null,
        string? description = null)
    {
        if (locationId == Guid.Empty)
            throw new ArgumentException("O id do local é obrigatório.", nameof(locationId));

        var device = new Device();
        device.LocationId = locationId;
        device.Update(name, type, rackId, ip, mac, serial, model, vendor, vlanId, description);
        device._monitoringChecks = [.. DeviceProfileDefaults.For(type)];
        device.ProfileIsCustom = false;

        // ADR-030 §5 — o Create delega ao Update; descarta o Updated
        // transitório e publica só o Created.
        device.ClearDomainEvents();
        device.RaiseDomainEvent(new DeviceCreatedDomainEvent(device.Id, device.Name, device.Type));
        return device;
    }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new DeviceDeletedDomainEvent(Id, Name, Type));

    public void Update(
        string name,
        DeviceType type,
        Guid? rackId,
        string? ip = null,
        string? mac = null,
        string? serial = null,
        string? model = null,
        string? vendor = null,
        Guid? vlanId = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do device é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome do device pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        var normalizedIp = NormalizeOptional(ip, IpMaxLength, "O IP do device", nameof(ip));
        if (normalizedIp is not null && !IsValidIp(normalizedIp))
            throw new ArgumentException("O IP informado é inválido.", nameof(ip));

        var normalizedMac = NormalizeOptional(mac, MacMaxLength, "O MAC do device", nameof(mac));
        if (normalizedMac is not null)
        {
            normalizedMac = NormalizeMac(normalizedMac)
                ?? throw new ArgumentException("O MAC informado é inválido.", nameof(mac));
        }

        var normalizedSerial = NormalizeOptional(serial, SerialMaxLength, "O serial do device", nameof(serial));
        var normalizedModel = NormalizeOptional(model, ModelMaxLength, "O modelo do device", nameof(model));
        var normalizedVendor = NormalizeOptional(vendor, VendorMaxLength, "O fabricante do device", nameof(vendor));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição do device pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        Name = name;
        Type = type;
        RackId = rackId;
        Ip = normalizedIp;
        Mac = normalizedMac;
        Serial = normalizedSerial;
        Model = normalizedModel;
        Vendor = normalizedVendor;
        VlanId = vlanId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        // ADR-014: tipo pode mudar, mas override explícito do operador é preservado.

        RaiseDomainEvent(new DeviceUpdatedDomainEvent(Id, Name, Type));
    }

    /// <summary>Override dos checks (F2-T008). Exige ao menos um check sem duplicatas.</summary>
    public void ApplyCustomProfile(IEnumerable<CheckKind> checks)
    {
        var list = checks.Distinct().ToList();
        if (list.Count == 0)
            throw new ArgumentException("Informe ao menos um check.", nameof(checks));

        if (list.Count != checks.Count())
            throw new ArgumentException("Checks duplicados não são permitidos.", nameof(checks));

        if (list.Count > MaxMonitoringChecks)
            throw new ArgumentException(
                $"O device pode ter no máximo {MaxMonitoringChecks} checks.", nameof(checks));

        _monitoringChecks = list;
        ProfileIsCustom = true;
    }

    /// <summary>Volta aos defaults do <see cref="Type"/> atual (F2-T008).</summary>
    public void ResetProfileToDefault()
    {
        _monitoringChecks = [.. DeviceProfileDefaults.For(Type)];
        ProfileIsCustom = false;
    }

    internal void ReplaceMonitoringChecksForEf(IReadOnlyList<CheckKind> checks, bool isCustom)
    {
        _monitoringChecks = [.. checks];
        ProfileIsCustom = isCustom;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string label, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException(
                $"{label} pode ter no máximo {maxLength} caracteres.", paramName);

        return trimmed;
    }

    private static bool IsValidIp(string value)
        => IPAddress.TryParse(value, out var address)
           && address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;

    private static string? NormalizeMac(string value)
    {
        var compact = value.Replace("-", string.Empty).Replace(":", string.Empty).ToLowerInvariant();
        if (compact.Length != 12 || compact.Any(c => !Uri.IsHexDigit(c)))
            return null;

        return string.Join(':', Enumerable.Range(0, 6).Select(i => compact.Substring(i * 2, 2)));
    }
}
