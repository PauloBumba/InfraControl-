namespace InfraControl.Api.ViewModels.Identity;

public sealed record AcceptInvitationViewModel(string Token);

public sealed record SetInvitationPasswordViewModel(string Token, string Password);
