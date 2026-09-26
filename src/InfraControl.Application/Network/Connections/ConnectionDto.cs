namespace InfraControl.Application.Network.Connections;

/// <summary>Conexão dirigida com as pontas resolvidas (F3-T005).</summary>
public sealed record ConnectionDto(
    Guid Id,
    Guid SourceInterfaceId,
    string SourceInterfaceName,
    Guid SourceDeviceId,
    Guid TargetInterfaceId,
    string TargetInterfaceName,
    Guid TargetDeviceId);
