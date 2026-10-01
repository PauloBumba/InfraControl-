using FluentAssertions;
using InfraControl.Api.Workers;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.Telegram;
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
/// F9-T006: NotificationTelegramWorker — Pending → Sent no envio OK,
/// → Failed no envio falho; destino = ChatId da config global
/// (Notifications:Channels:Telegram — decisão do usuário: admin escolhe
/// onde enviar, sem destino por usuário).
/// </summary>
public sealed class NotificationTelegramWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationTelegramWorkerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FakeDispatcher : ITelegramNotificationDispatcher
    {
        public List<TelegramNotificationMessage> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendAsync(
            TelegramNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("bot offline");

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private async Task<(AppDbContext Context, NotificationTelegramWorker Worker, FakeDispatcher Dispatcher, Guid DeliveryId)> CreateAsync(
        string chatId = "-1001234")
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "telegram-worker@exemplo.test", "Worker Telegram");
        context.DomainUsers.Add(user);
        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", "Mensagem.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var delivery = NotificationDelivery.Create(notification.Id, user.Id, NotificationChannel.Telegram);
        context.NotificationDeliveries.Add(delivery);
        await context.SaveChangesAsync();

        var dispatcher = new FakeDispatcher();
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => new AppDbContext(_options));
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
        services.AddScoped<ITelegramNotificationDispatcher>(_ => dispatcher);
        var provider = services.BuildServiceProvider();

        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Telegram"],
            Telegram = new TelegramChannelOptions { ChatId = chatId },
        };

        var worker = new NotificationTelegramWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(channels),
            NullLogger<NotificationTelegramWorker>.Instance);

        return (context, worker, dispatcher, delivery.Id);
    }

    [Fact]
    public async Task ProcessPending_WhenSendSucceeds_ShouldMarkSentWithChatId()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent.Should().ContainSingle(m => m.DeliveryId == deliveryId);
        dispatcher.Sent[0].ChatId.Should().Be("-1001234");
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
        delivery.LastError.Should().Be("bot offline");
    }

    /// <summary>
    /// Config mudou entre enfileirar e despachar (ChatId esvaziado): a
    /// delivery vira Failed com erro pt-BR apontando a seção — F9-T009
    /// cuidará do retry.
    /// </summary>
    [Fact]
    public async Task ProcessPending_WithoutChatId_ShouldMarkFailedWithConfigError()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync(chatId: "");

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        dispatcher.Sent.Should().BeEmpty();
        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.LastError.Should().Contain("ChatId");
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
