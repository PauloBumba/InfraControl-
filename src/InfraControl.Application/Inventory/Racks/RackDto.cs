namespace InfraControl.Application.Inventory.Racks;

/// <summary>
/// Payload de Rack devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record RackDto(Guid Id, Guid LocationId, string Name, string? Description);
