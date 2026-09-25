namespace InfraControl.Application.Network.SwitchPorts;

/// <summary>
/// Payload de SwitchPort devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record SwitchPortDto(
    Guid Id,
    Guid DeviceId,
    string Name,
    string? Description,
    bool? LinkUp);
