namespace InfraControl.Application.Inventory.Locations;

/// <summary>
/// Payload de Location devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record LocationDto(Guid Id, string Name, string? Description);
