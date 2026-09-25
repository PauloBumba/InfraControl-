namespace InfraControl.Application.Inventory.IpAddresses;

/// <summary>
/// Payload de IpAddress devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record IpAddressDto(Guid Id, Guid InterfaceId, string Ip);
