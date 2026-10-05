using InfraControl.Domain.Enums;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

public sealed class User : AggregateRoot
{
    private readonly List<UserRoleAssignment> _roleAssignments = [];

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roleAssignments.Select(assignment => assignment.Role).ToArray();
    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    private User() { }

    private User(Guid id, string email, string displayName, UserRole initialRole, bool isActive)
        : base(id)
    {
        Email = NormalizeEmail(email);
        DisplayName = RequireDisplayName(displayName);
        IsActive = isActive;
        _roleAssignments.Add(new UserRoleAssignment(Id, initialRole));
        RaiseDomainEvent(new UserCreatedDomainEvent(Id, Email));
    }

    public static User Create(string email, string displayName, UserRole initialRole = UserRole.Viewer)
        => new(Guid.NewGuid(), email, displayName, initialRole, isActive: true);

    public static User Create(Guid id, string email, string displayName, UserRole initialRole = UserRole.Viewer)
    {
        if (id == Guid.Empty) throw new ArgumentException("O id do usuário é obrigatório.", nameof(id));
        return new(id, email, displayName, initialRole, isActive: true);
    }

    /// <summary>
    /// Cria uma conta INATIVA. Usuários criados fora de um passo explícito de
    /// ativação (auto-registro e aceite de convite) só ficam ativos após
    /// definição de senha via convite ou ativação manual por administrador.
    /// </summary>
    public static User CreatePending(Guid id, string email, string displayName, IEnumerable<UserRole> roles)
    {
        if (id == Guid.Empty) throw new ArgumentException("O id do usuário é obrigatório.", nameof(id));

        var distinctRoles = (roles ?? []).Distinct().ToArray();
        if (distinctRoles.Length == 0)
            throw new ArgumentException("Ao menos uma função é obrigatória.", nameof(roles));

        var user = new User(id, email, displayName, distinctRoles[0], isActive: false);
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

    public void Update(string displayName, string email)
    {
        var newEmail = NormalizeEmail(email);
        var newDisplayName = RequireDisplayName(displayName);

        if (Email == newEmail && DisplayName == newDisplayName)
            return;

        Email = newEmail;
        DisplayName = newDisplayName;
        Touch();
        RaiseDomainEvent(new UserUpdatedDomainEvent(Id, DisplayName, Email));
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
}
