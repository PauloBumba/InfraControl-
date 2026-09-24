namespace InfraControl.Api.ViewModels.Inventory;

public sealed record CreateDeviceViewModel(
    Guid LocationId,
    string Name,
    string Type,
    Guid? RackId = null,
    string? Ip = null,
    string? Mac = null,
    string? Serial = null,
    string? Model = null,
    string? Vendor = null,
    Guid? VlanId = null,
    string? Description = null);

public sealed record UpdateDeviceViewModel(
    string Name,
    string Type,
    Guid? RackId = null,
    string? Ip = null,
    string? Mac = null,
    string? Serial = null,
    string? Model = null,
    string? Vendor = null,
    Guid? VlanId = null,
    string? Description = null);

/// <summary>F2-T008: envia lista de checks (enum CheckKind) ou useDefault=true.</summary>
public sealed record UpdateDeviceProfileViewModel(
    string[]? Checks = null,
    bool UseDefault = false);
