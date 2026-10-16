namespace InfraControl.Application.Inventory.CameraNvrMappings;

/// <summary>
/// Payload de CameraNvrMapping devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record CameraNvrMappingDto(
    Guid Id,
    Guid CameraId,
    Guid NvrId,
    int ChannelNumber);
