namespace InfraControl.Api.ViewModels.Network;

public sealed record CreateVlanViewModel(string Name, int VlanNumber, string? Description = null);

public sealed record UpdateVlanViewModel(string Name, int VlanNumber, string? Description);
