namespace InfraControl.Application.Network.Vlans;

/// <summary>
/// Payload de Vlan devolvido pela API (sucesso → data do envelope).
/// </summary>
public sealed record VlanDto(
    Guid Id,
    string Name,
    int VlanNumber,
    string? Description);
