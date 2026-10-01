using FluentAssertions;
using InfraControl.Api.Workers;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.Email;
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
/// F9-T005: NotificationEmailWorker — Pending → Sent no envio OK,
/// → Failed com erro no envio falho; destinatário = e-mail do usuário.
/// </summary>
public sealed class NotificationEmailWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationEmailWorkerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FakeDispatcher : IEmailNotificationDispatcher
    {
        public List<EmailNotificationMessage> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendAsync(
            EmailNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("smtp indisponível");

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private async Task<(AppDbContext Context, NotificationEmailWorker Worker, FakeDispatcher Dispatcher, Guid DeliveryId)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "email-worker@exemplo.test", "Worker Email");
        context.DomainUsers.Add(user);
        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", "Mensagem.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var delivery = NotificationDelivery.Create(notification.Id, user.Id, NotificationChannel.Email);
        context.NotificationDeliveries.Add(delivery);
        await context.SaveChangesAsync();

        var dispatcher = new FakeDispatcher();
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => new AppDbContext(_options));
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
        services.AddScoped<IEmailNotificationDispatcher>(_ => dispatcher);
        var provider = services.BuildServiceProvider();

        var worker = new NotificationEmailWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationEmailWorker>.Instance);

        return (context, worker, dispatcher, delivery.Id);
    }

    [Fact]
    public async Task ProcessPending_WhenSendSucceeds_ShouldMarkSentWithRecipientEmail()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent.Should().ContainSingle(m => m.DeliveryId == deliveryId);
        dispatcher.Sent[0].To.Should().Be("email-worker@exemplo.test");
        dispatcher.Sent[0].Subject.Should().Be("Título");

        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPending_WhenSendFails_ShouldMarkFailedWithPtBrError()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();
        dispatcher.Fail = true;

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.LastError.Should().Be("smtp indisponível");
    }

    [Fact]
    public async Task ProcessPending_WithoutPending_ShouldReturnZero()
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
