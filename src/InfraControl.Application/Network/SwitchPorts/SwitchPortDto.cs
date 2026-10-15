using InfraControl.Domain.Enums;

namespace InfraControl.Application.Network.SwitchPorts;

/// <summary>
/// Payload de SwitchPort devolvido pela API (sucesso → data do envelope).
/// Speed/Duplex entram como enum serializado como texto (F30-T001 S4a — o
/// JsonConverter por tipo manda "Speed1G"/"Full"; null = ainda não observado).
/// </summary>
public sealed record SwitchPortDto(
    Guid Id,
    Guid DeviceId,
    string Name,
    string? Description,
    bool? LinkUp,
    SwitchPortSpeed? Speed,
    SwitchPortDuplex? Duplex);
