namespace InfraControl.Application.Inventory.Devices;

/// <summary>
/// Payload de Device devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record DeviceDto(
    Guid Id,
    Guid LocationId,
    Guid? RackId,
    string Type,
    string Name,
    string? Ip,
    string? Mac,
    string? Serial,
    string? Model,
    string? Vendor,
    Guid? VlanId,
    string? Description);
