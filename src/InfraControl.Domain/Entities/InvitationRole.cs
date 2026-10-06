using InfraControl.Domain.Enums;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

public sealed class InvitationRole : Entity
{
    public Guid InvitationId { get; private set; }
    public UserRole Role { get; private set; }

    private InvitationRole() { }

    internal InvitationRole(Guid invitationId, UserRole role)
    {
        InvitationId = invitationId;
        Role = role;
    }

    /// <summary>
    /// ADR-030 §1 — fábrica para o repositório trocar as roles de um
    /// convite (replace físico no PUT pendente).
    /// </summary>
    public static InvitationRole Assign(Guid invitationId, UserRole role)
        => new(invitationId, role);
}
