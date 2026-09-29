using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T003: NotificationPreference — invariantes de usuário/canal e
/// ativação/desativação.
/// </summary>
public sealed class NotificationPreferenceTests
{
    [Fact]
    public void Create_ShouldPersistUserChannelAndEnabled()
    {
        var userId = Guid.NewGuid();

        var preference = NotificationPreference.Create(
            userId, NotificationChannel.Email, enabled: true);

        preference.UserId.Should().Be(userId);
        preference.Channel.Should().Be(NotificationChannel.Email);
        preference.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => NotificationPreference.Create(
            Guid.Empty, NotificationChannel.SignalR, enabled: true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do usuário é obrigatório.*");
    }

    [Fact]
    public void Create_UndefinedChannel_ShouldThrowArgumentExceptionPtBr()
    {
        var act = () => NotificationPreference.Create(
            Guid.NewGuid(), (NotificationChannel)99, enabled: true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Canal de notificação inválido: '99'.*");
    }

    [Fact]
    public void SetEnabled_ShouldTogglePreference()
    {
        var preference = NotificationPreference.Create(
            Guid.NewGuid(), NotificationChannel.Telegram, enabled: true);

        preference.SetEnabled(false);

        preference.Enabled.Should().BeFalse();
    }
}
