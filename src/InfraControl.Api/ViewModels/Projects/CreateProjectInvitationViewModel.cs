using InfraControl.Domain.Enums;

namespace InfraControl.Api.ViewModels.Projects;

public sealed record CreateProjectInvitationViewModel(
    string Email,
    ProjectMemberRole Role,
    int ExpiresInDays = 7);
