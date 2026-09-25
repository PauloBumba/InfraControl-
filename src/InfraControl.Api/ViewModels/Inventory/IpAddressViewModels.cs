namespace InfraControl.Api.ViewModels.Inventory;

public sealed record CreateIpAddressViewModel(Guid InterfaceId, string Ip);

public sealed record UpdateIpAddressViewModel(string Ip);
