using InfraControl.Domain.Enums;

namespace InfraControl.Application.Network.Connections;

/// <summary>
/// Conexão dirigida com as pontas resolvidas (F3-T005) + origem e
/// confiança da informação (F53-T001, spec 11.7).
/// </summary>
public sealed record ConnectionDto(
    Guid Id,
    Guid SourceInterfaceId,
    string SourceInterfaceName,
    Guid SourceDeviceId,
    Guid TargetInterfaceId,
    string TargetInterfaceName,
    Guid TargetDeviceId,
    string Source,
    string Confidence);

/// <summary>
/// Wire das origens/confiança em minúsculas (F53-T001 — decisão registrada;
/// casing igual ao UniFi live: "lldp"/"confirmed", casando sem normalizar
/// na topologia F53). ADR-061: enum viaja como string — aqui lowercase.
/// </summary>
internal static class ConnectionWire
{
    public static string ToWire(this ConnectionSource source)
        => source.ToString().ToLowerInvariant();

    public static string ToWire(this ConnectionConfidence confidence)
        => confidence.ToString().ToLowerInvariant();
}
