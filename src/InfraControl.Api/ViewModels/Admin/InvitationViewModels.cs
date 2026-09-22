using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Admin;

public sealed record CreateInvitationViewModel(
    string Email,
    IReadOnlyCollection<UserRole> Roles,
    int ExpiresInDays = 7);
