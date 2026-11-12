using InfraControl.Domain.Enums;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

public sealed class User : AggregateRoot
{
    private readonly List<UserRoleAssignment> _roleAssignments = [];

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? FullName { get; private set; }
    /// <summary>
    /// Caminho web da foto de perfil (<c>/uploads/avatars/…</c>, servido por
    /// <c>UseStaticFiles</c>). Null = iniciais na UI. Só caminhos do módulo
    /// de avatars são aceitos — nunca URL arbitrária (evita hotlink/SSRF de
    /// exibição e mantém o arquivo dentro do wwwroot da API).
    /// </summary>
    public string? AvatarPath { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roleAssignments.Select(assignment => assignment.Role).ToArray();
    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    private User() { }

    private User(Guid id, string email, string displayName, string? fullName, UserRole initialRole, bool isActive)
        : base(id)
    {
        Email = NormalizeEmail(email);
        DisplayName = RequireDisplayName(displayName);
        FullName = NormalizeFullName(fullName);
        IsActive = isActive;
        _roleAssignments.Add(new UserRoleAssignment(Id, initialRole));
        RaiseDomainEvent(new UserCreatedDomainEvent(Id, Email));
    }

    public static User Create(string email, string displayName, UserRole initialRole)
        => new(Guid.NewGuid(), email, displayName, null, initialRole, isActive: true);

    public static User Create(string email, string displayName, string? fullName = null, UserRole initialRole = UserRole.Viewer)
        => new(Guid.NewGuid(), email, displayName, fullName, initialRole, isActive: true);

    public static User Create(Guid id, string email, string displayName, UserRole initialRole = UserRole.Viewer)
        => new(id, email, displayName, null, initialRole, isActive: true);

    public static User Create(Guid id, string email, string displayName, string? fullName, UserRole initialRole = UserRole.Viewer)
    {
        if (id == Guid.Empty) throw new ArgumentException("O id do usuário é obrigatório.", nameof(id));
        return new(id, email, displayName, fullName, initialRole, isActive: true);
    }

    /// <summary>
    /// Cria uma conta INATIVA. Usuários criados fora de um passo explícito de
    /// ativação (auto-registro e aceite de convite) só ficam ativos após
    /// definição de senha via convite ou ativação manual por administrador.
    /// </summary>
    public static User CreatePending(Guid id, string email, string displayName, IEnumerable<UserRole> roles)
        => CreatePending(id, email, displayName, null, roles);

    public static User CreatePending(Guid id, string email, string displayName, string? fullName, IEnumerable<UserRole> roles)
    {
        if (id == Guid.Empty) throw new ArgumentException("O id do usuário é obrigatório.", nameof(id));

        var distinctRoles = (roles ?? []).Distinct().ToArray();
        if (distinctRoles.Length == 0)
            throw new ArgumentException("Ao menos uma função é obrigatória.", nameof(roles));

        var user = new User(id, email, displayName, fullName, distinctRoles[0], isActive: false);
        foreach (var role in distinctRoles.Skip(1))
            user._roleAssignments.Add(new UserRoleAssignment(user.Id, role));

        return user;
    }

    public void AssignRole(UserRole role)
    {
        if (_roleAssignments.All(assignment => assignment.Role != role))
        {
            _roleAssignments.Add(new UserRoleAssignment(Id, role));
            Touch();
            RaiseDomainEvent(new UserRoleAssignedDomainEvent(Id, role));
        }
    }

    public void RemoveRole(UserRole role)
    {
        var assignment = _roleAssignments.SingleOrDefault(current => current.Role == role);
        if (assignment is null) return;
        if (_roleAssignments.Count == 1)
            throw new InvalidOperationException("O usuário deve manter ao menos uma função.");

        _roleAssignments.Remove(assignment);
        Touch();
        RaiseDomainEvent(new UserRoleRemovedDomainEvent(Id, role));
    }

    public void Disable()
    {
        if (!IsActive) return;
        IsActive = false;
        Touch();
        RaiseDomainEvent(new UserDisabledDomainEvent(Id));
    }

    public void Enable()
    {
        if (IsActive) return;
        IsActive = true;
        Touch();
        RaiseDomainEvent(new UserEnabledDomainEvent(Id));
    }

    public void Update(string displayName, string email, string? fullName = null)
    {
        var newEmail = NormalizeEmail(email);
        var newDisplayName = RequireDisplayName(displayName);
        var newFullName = NormalizeFullName(fullName);

        if (Email == newEmail && DisplayName == newDisplayName && FullName == newFullName)
            return;

        Email = newEmail;
        DisplayName = newDisplayName;
        FullName = newFullName;
        Touch();
        RaiseDomainEvent(new UserUpdatedDomainEvent(Id, DisplayName, Email));
    }

    /// <summary>
    /// F51 — foto de perfil auto-editável. <paramref name="avatarPath"/> null
    /// ou vazio REMOVE a foto; qualquer outro valor precisa ser um caminho
    /// <c>/uploads/avatars/…</c> relativo (nunca absoluto nem de outro módulo).
    /// O serviço só chama este método quando o campo foi informado no request —
    /// "não informado" (ausente) preserva o valor, como FullName.
    /// </summary>
    public void SetAvatarPath(string? avatarPath)
    {
        var normalized = string.IsNullOrWhiteSpace(avatarPath) ? null : avatarPath.Trim();
        if (normalized is not null
            && (!normalized.StartsWith("/uploads/avatars/", StringComparison.Ordinal)
                || normalized.Contains("..", StringComparison.Ordinal)
                || normalized.Length > 500))
        {
            throw new ArgumentException(
                "Avatar inválido — envie a imagem pelo upload da plataforma.",
                nameof(avatarPath));
        }

        if (AvatarPath == normalized) return;
        AvatarPath = normalized;
        Touch();
    }

    public void Delete()
    {
        // Guard: usuário não pode se auto-excluir - validado no serviço
        // Evento disparado ANTES de marcar deleted para notificação
        RaiseDomainEvent(new UserDeletedDomainEvent(Id, Email, DisplayName));
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Um e-mail válido é obrigatório.", nameof(email));

        return email.Trim().ToLowerInvariant();
    }

    private static string RequireDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("O nome de exibição é obrigatório.", nameof(displayName));

        return displayName.Trim();
    }

    private static string? NormalizeFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        var trimmed = fullName.Trim();
        return trimmed.Length > 200 ? trimmed[..200] : trimmed;
    }
}
