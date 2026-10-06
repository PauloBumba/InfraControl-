using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Admin;

public sealed record CreateInvitationViewModel(
    string Email,
    IReadOnlyCollection<UserRole> Roles,
    int ExpiresInDays = 7);

/// <summary>PUT do convite pendente (ADR-030 §1).</summary>
public sealed record UpdateInvitationViewModel(IReadOnlyCollection<UserRole> Roles);

