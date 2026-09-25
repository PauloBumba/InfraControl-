namespace InfraControl.Application.Inventory.MacAddresses;

/// <summary>
/// Payload de MacAddress devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record MacAddressDto(Guid Id, Guid InterfaceId, string Mac);
