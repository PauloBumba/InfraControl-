using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T002: NotificationRecipient — usuário OU role (XOR), invariantes
/// pt-BR e dedup por índice único.
/// </summary>
public sealed class NotificationRecipientTests
{
    private static readonly Guid NotificationId = Guid.NewGuid();

    [Fact]
    public void CreateForUser_ShouldPersistNotificationAndUser()
    {
        var userId = Guid.NewGuid();

        var recipient = NotificationRecipient.CreateForUser(NotificationId, userId);

        recipient.NotificationId.Should().Be(NotificationId);
        recipient.UserId.Should().Be(userId);
        recipient.Role.Should().BeNull();
    }

    [Fact]
    public void CreateForRole_ShouldPersistRoleName()
    {
        var recipient = NotificationRecipient.CreateForRole(
            NotificationId, UserRole.Operator);

        recipient.Role.Should().Be("Operator");
        recipient.UserId.Should().BeNull();
        recipient.NotificationId.Should().Be(NotificationId);
    }

    [Fact]
    public void CreateForRole_FromString_ShouldNormalizeToRoleEnum()
    {
        var recipient = NotificationRecipient.CreateForRole(NotificationId, "viewer");

        recipient.Role.Should().Be("Viewer");
    }

    [Fact]
    public void CreateForUser_EmptyNotificationId_ShouldThrowPtBr()
    {
        var act = () => NotificationRecipient.CreateForUser(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id da notificação é obrigatório.*");
    }

    [Fact]
    public void CreateForUser_EmptyUserId_ShouldThrowPtBr()
    {
        var act = () => NotificationRecipient.CreateForUser(NotificationId, Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do usuário é obrigatório.*");
    }

    [Fact]
    public void CreateForRole_UnknownRole_ShouldThrowPtBr()
    {
        var act = () => NotificationRecipient.CreateForRole(NotificationId, "SuperUser");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Role inválida: 'SuperUser'.*");
    }
}
