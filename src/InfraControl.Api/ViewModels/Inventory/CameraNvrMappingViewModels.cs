namespace InfraControl.Api.ViewModels.Inventory;

/// <summary>
/// F30-T005c: payloads de CameraNvrMapping (docs/46 §Modelo de domínio).
/// </summary>
public sealed record CreateCameraNvrMappingViewModel(
    Guid CameraId,
    Guid NvrId,
    int ChannelNumber);

public sealed record UpdateCameraNvrMappingViewModel(
    Guid CameraId,
    Guid NvrId,
    int ChannelNumber);
