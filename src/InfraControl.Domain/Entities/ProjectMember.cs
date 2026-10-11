using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Membro participante da equipe do Projeto (ADR-043 · docs/38 §3.3).
/// </summary>
public sealed class ProjectMember : Entity
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectMemberRole Role { get; private set; }

    private ProjectMember() { }

    public static ProjectMember Create(Guid projectId, Guid userId, ProjectMemberRole role)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("O ID do projeto é obrigatório.", nameof(projectId));

        if (userId == Guid.Empty)
            throw new ArgumentException("O ID do usuário participante é obrigatório.", nameof(userId));

        return new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role,
        };
    }

    public void UpdateRole(ProjectMemberRole role)
    {
        Role = role;
        Touch();
    }
}
