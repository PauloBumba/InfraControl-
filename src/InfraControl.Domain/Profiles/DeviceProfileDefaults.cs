using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Profiles;

/// <summary>
/// Defaults de checks por DeviceType (ADR-014). Fonte única de verdade em
/// código — mudar default de tipo exige release.
/// </summary>
public static class DeviceProfileDefaults
{
    private static readonly IReadOnlyDictionary<DeviceType, IReadOnlyList<CheckKind>> ByType =
        new Dictionary<DeviceType, IReadOnlyList<CheckKind>>
        {
            [DeviceType.Printer] = [CheckKind.Ping, CheckKind.SnmpToner, CheckKind.SnmpDrum],
            [DeviceType.AccessPoint] = [CheckKind.Ping, CheckKind.HttpWan],
            [DeviceType.Switch] = [CheckKind.Ping, CheckKind.SnmpSystem],
            [DeviceType.Server] = [CheckKind.Ping, CheckKind.SnmpSystem],
            // F5-T006: câmera monitora a porta 37777 por padrão.
            [DeviceType.Camera] = [CheckKind.Ping, CheckKind.Tcp],
            [DeviceType.Router] = [CheckKind.Ping, CheckKind.SnmpSystem],
            [DeviceType.Firewall] = [CheckKind.Ping, CheckKind.SnmpSystem],
            [DeviceType.Workstation] = [CheckKind.Ping],
            [DeviceType.Phone] = [CheckKind.Ping],
            [DeviceType.UPS] = [CheckKind.Ping, CheckKind.SnmpSystem],
            [DeviceType.Other] = [CheckKind.Ping]
        };

    public static IReadOnlyList<CheckKind> For(DeviceType type)
        => ByType.TryGetValue(type, out var checks)
            ? checks
            : [CheckKind.Ping];
}
