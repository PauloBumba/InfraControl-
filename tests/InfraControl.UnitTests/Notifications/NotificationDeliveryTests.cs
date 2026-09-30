using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T004: NotificationDelivery — invariantes pt-BR e transições
/// Sent/Failed (retry completo em F9-T009).
/// </summary>
public sealed class NotificationDeliveryTests
{
    private static readonly Guid NotificationId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldPersistNotificationUserAndChannelAsPending()
    {
        var userId = Guid.NewGuid();

        var delivery = NotificationDelivery.Create(
            NotificationId, userId, NotificationChannel.SignalR);

        delivery.NotificationId.Should().Be(NotificationId);
        delivery.UserId.Should().Be(userId);
        delivery.Channel.Should().Be(NotificationChannel.SignalR);
        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.SentAt.Should().BeNull();
        delivery.LastError.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyNotificationId_ShouldThrowPtBr()
    {
        var act = () => NotificationDelivery.Create(
            Guid.Empty, Guid.NewGuid(), NotificationChannel.Email);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id da notificação é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowPtBr()
    {
        var act = () => NotificationDelivery.Create(
            NotificationId, Guid.Empty, NotificationChannel.Email);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do usuário é obrigatório.*");
    }

    [Fact]
    public void Create_UndefinedChannel_ShouldThrowPtBr()
    {
        var act = () => NotificationDelivery.Create(
            NotificationId, Guid.NewGuid(), (NotificationChannel)99);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Canal de notificação inválido: '99'.*");
    }

    [Fact]
    public void MarkSent_ShouldSetStatusAndTimestampAndClearError()
    {
        var delivery = NotificationDelivery.Create(
            NotificationId, Guid.NewGuid(), NotificationChannel.SignalR);
        delivery.MarkFailed("falhou");

        var sentAt = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        delivery.MarkSent(sentAt);

        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.SentAt.Should().Be(sentAt);
        delivery.LastError.Should().BeNull();
    }

    /// <summary>
    /// F9-T009: primeira falha agenda o próximo retry com backoff
    /// exponencial (base 30s) e mantém Status = Failed.
    /// </summary>
    [Fact]
    public void RegisterFailedAttempt_FirstFailure_ShouldScheduleRetryWithBackoff()
    {
        var delivery = NotificationDelivery.Create(
            NotificationId, Guid.NewGuid(), NotificationChannel.Email);
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        delivery.RegisterFailedAttempt(
            "smtp offline", now, maxAttempts: 5, backoff: TimeSpan.FromSeconds(30));

        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(1);
        delivery.NextAttemptAt.Should().Be(now.AddSeconds(30));
        delivery.LastError.Should().Be("smtp offline");
    }

    /// <summary>Backoff dobra a cada tentativa (30 → 60 → 120 → 240s).</summary>
    [Fact]
    public void RegisterFailedAttempt_BackoffShouldDoubleEachAttempt()
    {
        var delivery = NotificationDelivery.Create(
            NotificationId, Guid.NewGuid(), NotificationChannel.Email);
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        delivery.RegisterFailedAttempt("falha", now, 5, TimeSpan.FromSeconds(30));
        delivery.RegisterFailedAttempt("falha", now, 5, TimeSpan.FromSeconds(30));
        delivery.RegisterFailedAttempt("falha", now, 5, TimeSpan.FromSeconds(30));

        delivery.AttemptCount.Should().Be(3);
        delivery.NextAttemptAt.Should().Be(now.AddSeconds(120));
    }

    /// <summary>
    /// Esgotou as tentativas = DLQ: Status Failed e SEM próximo retry
    /// (o worker não volta a escolher a delivery).
    /// </summary>
    [Fact]
    public void RegisterFailedAttempt_AtMaxAttempts_ShouldEnterDlqWithoutNextRetry()
    {
        var delivery = NotificationDelivery.Create(
            NotificationId, Guid.NewGuid(), NotificationChannel.Email);
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 5; i++)
            delivery.RegisterFailedAttempt("falha", now, 5, TimeSpan.FromSeconds(30));

        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(5);
        delivery.NextAttemptAt.Should().BeNull();
        delivery.LastError.Should().Be("falha");
    }

    [Fact]
    public void MarkFailed_ShouldTruncateErrorTo500()
    {
        var delivery = NotificationDelivery.Create(
            NotificationId, Guid.NewGuid(), NotificationChannel.SignalR);

        delivery.MarkFailed(new string('x', 600));

        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.LastError!.Length.Should().Be(500);
    }
}
