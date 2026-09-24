namespace InfraControl.Application.Inventory.NetworkInterfaces;

/// <summary>
/// Payload de NetworkInterface devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record NetworkInterfaceDto(Guid Id, Guid DeviceId, string Name, string? Description);
