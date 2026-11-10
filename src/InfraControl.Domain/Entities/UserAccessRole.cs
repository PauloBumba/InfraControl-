using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Atribuição de perfil customizado (<see cref="AccessRole"/>) a um
/// usuário. Os 3 perfis embutidos (Viewer/Operator/Admin) continuam em
/// <c>UserRoleAssignment</c> — esta tabela guarda APENAS perfis criados
/// pelo admin. A chave do perfil viaja como claim <c>ClaimTypes.Role</c>
/// junto com os papéis do enum, então toda a resolução de acesso
/// (HTTP + MediatR) enxerga os dois mundos sem bifurcação.
/// </summary>
public sealed class UserAccessRole : Entity
{
    public Guid UserId { get; private set; }
    public string RoleKey { get; private set; } = string.Empty;

    private UserAccessRole() { }

    public static UserAccessRole Assign(Guid userId, string roleKey)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));
        var normalized = AccessRole.NormalizeKey(roleKey);
        return new UserAccessRole { UserId = userId, RoleKey = normalized };
    }
}
