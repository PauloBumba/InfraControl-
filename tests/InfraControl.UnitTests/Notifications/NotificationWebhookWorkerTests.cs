using FluentAssertions;
using InfraControl.Api.Workers;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.Webhook;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T008: NotificationWebhookWorker — Pending → Sent no envio OK,
/// → Failed no envio falho; destino = Url da config global
/// (Notifications:Channels:Webhook — espelho do WhatsApp, F9-T007).
/// </summary>
public sealed class NotificationWebhookWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationWebhookWorkerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FakeDispatcher : IWebhookNotificationDispatcher
    {
        public List<WebhookNotificationMessage> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendAsync(
            WebhookNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("endpoint offline");

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private async Task<(AppDbContext Context, NotificationWebhookWorker Worker, FakeDispatcher Dispatcher, Guid DeliveryId)> CreateAsync(
        string url = "https://exemplo.test/webhook")
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "webhook-worker@exemplo.test", "Worker Webhook");
        context.DomainUsers.Add(user);
        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", "Mensagem.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var delivery = NotificationDelivery.Create(notification.Id, user.Id, NotificationChannel.Webhook);
        context.NotificationDeliveries.Add(delivery);
        await context.SaveChangesAsync();

        var dispatcher = new FakeDispatcher();
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => new AppDbContext(_options));
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
        services.AddScoped<IWebhookNotificationDispatcher>(_ => dispatcher);
        var provider = services.BuildServiceProvider();

        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Webhook"],
            Webhook = new WebhookChannelOptions { Url = url },
        };

        var worker = new NotificationWebhookWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(channels),
            NullLogger<NotificationWebhookWorker>.Instance);

        return (context, worker, dispatcher, delivery.Id);
    }

    [Fact]
    public async Task ProcessPending_WhenSendSucceeds_ShouldMarkSentWithUrl()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent.Should().ContainSingle(m => m.DeliveryId == deliveryId);
        dispatcher.Sent[0].Url.Should().Be("https://exemplo.test/webhook");
        dispatcher.Sent[0].Title.Should().Be("Título");
        dispatcher.Sent[0].Message.Should().Be("Mensagem.");

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
        delivery.LastError.Should().Be("endpoint offline");
    }

    /// <summary>
    /// Config mudou entre enfileirar e despachar (Url esvaziada): a
    /// delivery vira Failed com erro pt-BR apontando a seção — F9-T009
    /// cuidará do retry.
    /// </summary>
    [Fact]
    public async Task ProcessPending_WithoutUrl_ShouldMarkFailedWithConfigError()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync(url: "");

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        dispatcher.Sent.Should().BeEmpty();
        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.LastError.Should().Contain("Url");
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
