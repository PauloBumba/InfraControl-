namespace InfraControl.Api.ViewModels.Inventory;

public sealed record CreateMacAddressViewModel(Guid InterfaceId, string Mac);

public sealed record UpdateMacAddressViewModel(string Mac);
