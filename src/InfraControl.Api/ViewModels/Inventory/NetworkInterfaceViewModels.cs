namespace InfraControl.Api.ViewModels.Inventory;

public sealed record CreateNetworkInterfaceViewModel(Guid DeviceId, string Name, string? Description);

public sealed record UpdateNetworkInterfaceViewModel(string Name, string? Description);
