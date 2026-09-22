using InfraControl.Domain.Enums;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

public sealed class Invitation : AggregateRoot
{
    private readonly List<InvitationRole> _roles = [];

    public string Email { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public InvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles.Select(role => role.Role).ToArray();
    public IReadOnlyCollection<InvitationRole> RoleAssignments => _roles.AsReadOnly();

    private Invitation() { }

    private Invitation(Guid id, string email, string tokenHash, DateTime expiresAt, Guid createdBy, IEnumerable<UserRole> roles)
        : base(id)
    {
        Email = NormalizeEmail(email);
        TokenHash = RequireTokenHash(tokenHash);
        ExpiresAt = RequireFutureExpiry(expiresAt);
        CreatedBy = createdBy == Guid.Empty
            ? throw new ArgumentException("An inviter id is required.", nameof(createdBy))
            : createdBy;
        Status = InvitationStatus.Pending;

        var distinctRoles = roles.Distinct().ToArray();
        if (distinctRoles.Length == 0)
            throw new ArgumentException("At least one role is required.", nameof(roles));

        foreach (var role in distinctRoles)
            _roles.Add(new InvitationRole(Id, role));

        RaiseDomainEvent(new InvitationCreatedDomainEvent(Id, Email));
    }

    public static Invitation Create(string email, string tokenHash, DateTime expiresAt, Guid createdBy, IEnumerable<UserRole> roles)
        => new(Guid.NewGuid(), email, tokenHash, expiresAt, createdBy, roles);

    public bool IsExpired(DateTime utcNow) => Status == InvitationStatus.Pending && ExpiresAt <= utcNow;

    public void MarkExpired()
    {
        if (Status != InvitationStatus.Pending) return;
        Status = InvitationStatus.Expired;
        Touch();
    }

    public void Cancel()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Only pending invitations can be cancelled (current: {Status}).");

        Status = InvitationStatus.Cancelled;
        DecidedAt = DateTime.UtcNow;
        Touch();
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid email is required.", nameof(email));

        return email.Trim().ToLowerInvariant();
    }

    private static string RequireTokenHash(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("A token hash is required.", nameof(tokenHash));

        return tokenHash;
    }

    private static DateTime RequireFutureExpiry(DateTime expiresAt)
    {
        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiry must be in the future.", nameof(expiresAt));

        return expiresAt;
    }
}
