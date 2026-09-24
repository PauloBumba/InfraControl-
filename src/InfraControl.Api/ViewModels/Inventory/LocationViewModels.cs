namespace InfraControl.Api.ViewModels.Inventory;

public sealed record CreateLocationViewModel(string Name, string? Description);

public sealed record UpdateLocationViewModel(string Name, string? Description);

public sealed record CreateRackViewModel(Guid LocationId, string Name, string? Description);

public sealed record UpdateRackViewModel(string Name, string? Description);
