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
}
