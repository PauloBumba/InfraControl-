using FluentAssertions;
using InfraControl.Api.Workers;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.SignalR;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T004: NotificationSignalRWorker — Pending → Sent no push OK,
/// → Failed com erro no push falho; 0 sem pendentes.
/// </summary>
public sealed class NotificationSignalRWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationSignalRWorkerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FakeDispatcher : ISignalRNotificationDispatcher
    {
        public List<NotificationSignalRMessage> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendAsync(
            NotificationSignalRMessage message,
            CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("hub indisponível");

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private async Task<(AppDbContext Context, NotificationSignalRWorker Worker, FakeDispatcher Dispatcher, Guid DeliveryId)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "worker@exemplo.test", "Worker");
        context.DomainUsers.Add(user);
        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", "Mensagem.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var delivery = NotificationDelivery.Create(notification.Id, user.Id, NotificationChannel.SignalR);
        context.NotificationDeliveries.Add(delivery);
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => new AppDbContext(_options));
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
        var provider = services.BuildServiceProvider();

        var dispatcher = new FakeDispatcher();
        var worker = new NotificationSignalRWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            dispatcher,
            NullLogger<NotificationSignalRWorker>.Instance);

        return (context, worker, dispatcher, delivery.Id);
    }

    [Fact]
    public async Task ProcessPending_WhenPushSucceeds_ShouldMarkSent()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent.Should().ContainSingle(m => m.DeliveryId == deliveryId);
        dispatcher.Sent[0].Title.Should().Be("Título");

        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.SentAt.Should().NotBeNull();
        delivery.LastError.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPending_WhenPushFails_ShouldMarkFailedWithPtBrError()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();
        dispatcher.Fail = true;

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.LastError.Should().Be("hub indisponível");
        delivery.SentAt.Should().BeNull();
    }

    /// <summary>
    /// F9-T009: falha agenda retry — a mesma delivery volta a ser
    /// escolhida quando o `NextAttemptAt` vence e é despachada com
    /// sucesso na segunda tentativa.
    /// </summary>
    [Fact]
    public async Task ProcessPending_DueRetry_ShouldDispatchAgainAndMarkSent()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();
        dispatcher.Fail = true;
        (await worker.ProcessPendingAsync(CancellationToken.None)).Should().Be(0);

        dispatcher.Fail = false;
        var delivery = await context.NotificationDeliveries.SingleAsync(d => d.Id == deliveryId);
        delivery.RegisterFailedAttempt(
            "erro transitório",
            DateTime.UtcNow.AddDays(-1),
            maxAttempts: 5,
            backoff: TimeSpan.FromSeconds(30));
        await context.SaveChangesAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent.Should().ContainSingle(m => m.DeliveryId == deliveryId);
        delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.NextAttemptAt.Should().BeNull();
    }

    /// <summary>Retry ainda no futuro não é escolhida (backoff respeitado).</summary>
    [Fact]
    public async Task ProcessPending_RetryNotDue_ShouldSkipDelivery()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();
        dispatcher.Fail = true;
        (await worker.ProcessPendingAsync(CancellationToken.None)).Should().Be(0);
        dispatcher.Fail = false;

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        dispatcher.Sent.Should().BeEmpty();
        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.NextAttemptAt.Should().NotBeNull();
    }

    /// <summary>DLQ: esgotadas as tentativas a delivery não volta mais à fila.</summary>
    [Fact]
    public async Task ProcessPending_DlqDelivery_ShouldNeverBePickedAgain()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();
        var delivery = await context.NotificationDeliveries.SingleAsync(d => d.Id == deliveryId);
        for (var i = 0; i < 5; i++)
            delivery.RegisterFailedAttempt(
                "falha permanente",
                DateTime.UtcNow.AddDays(-1),
                maxAttempts: 5,
                backoff: TimeSpan.FromSeconds(30));
        await context.SaveChangesAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        dispatcher.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessPending_WithoutPending_ShouldReturnZeroAndNotCallDispatcher()
    {
        var (context, worker, dispatcher, _) = await CreateAsync();
        var delivery = await context.NotificationDeliveries.SingleAsync();
        delivery.MarkSent(DateTime.UtcNow);
        await context.SaveChangesAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        dispatcher.Sent.Should().BeEmpty();
    }
}
