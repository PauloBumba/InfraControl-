using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Projects;

/// <summary>
/// Regra de visibilidade de Projetos (spec evolução 2026-11-07 §5):
/// Administrador vê todos; demais usuários veem somente projetos onde são
/// líder, gestor, criador ou membro. A regra vive no backend — esconder na
/// interface não é autorização.
/// </summary>
public static class ProjectAccess
{
    public static bool IsAdmin(ICurrentUserContext user) =>
        user.Roles.Contains(UserRole.Admin.ToString());

    public static bool CanSee(Project project, ICurrentUserContext user)
    {
        if (IsAdmin(user))
            return true;

        if (user.UserId is not { } userId)
            return false;

        return project.LeaderId == userId
            || project.CreatedById == userId
            || project.ManagerId == userId
            || project.Members.Any(m => m.UserId == userId);
    }

    /// <summary>404 para projeto inexistente OU invisível — não vaza existência.</summary>
    public static Project EnsureVisible(Project? project, ICurrentUserContext user, Guid projectId)
    {
        if (project is null || !CanSee(project, user))
            throw new KeyNotFoundException($"Projeto '{projectId}' não encontrado.");

        return project;
    }
}
