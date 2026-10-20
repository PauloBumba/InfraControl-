using FluentAssertions;
using InfraControl.Api.Workers;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.WhatsApp;
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
/// F9-T007: NotificationWhatsAppWorker — Pending → Sent no envio OK,
/// → Failed no envio falho; destino = Phone da config global
/// (Notifications:Channels:WhatsApp — espelho do Telegram, F9-T006).
/// </summary>
public sealed class NotificationWhatsAppWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationWhatsAppWorkerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FakeDispatcher : IWhatsAppNotificationDispatcher
    {
        public List<WhatsAppNotificationMessage> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendAsync(
            WhatsAppNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("api offline");

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    /// <summary>F33-T005: fábrica de cartão (série Zabbix) controlável nos testes.</summary>
    private sealed class FakeChartFactory : IWhatsAppChartFactory
    {
        public IncidentChartData? Result { get; set; }
        public bool Throw { get; set; }
        public Guid? LastDeviceId { get; private set; }

        public Task<IncidentChartData?> TryCreateAsync(
            Guid? deviceId, string title, string eventType, DateTime eventAtUtc,
            CancellationToken cancellationToken = default)
        {
            LastDeviceId = deviceId;
            if (Throw)
                throw new InvalidOperationException("zabbix offline");
            return Task.FromResult(Result);
        }
    }

    private async Task<(AppDbContext Context, NotificationWhatsAppWorker Worker, FakeDispatcher Dispatcher, Guid DeliveryId)> CreateAsync(
        string phone = "+5511999990000",
        string groupId = "",
        FakeChartFactory? chartFactory = null,
        Guid? notificationDeviceId = null)
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var user = User.Create(Guid.NewGuid(), "whatsapp-worker@exemplo.test", "Worker WhatsApp");
        context.DomainUsers.Add(user);
        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", "Mensagem.",
            notificationDeviceId);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var delivery = NotificationDelivery.Create(notification.Id, user.Id, NotificationChannel.WhatsApp);
        context.NotificationDeliveries.Add(delivery);
        await context.SaveChangesAsync();

        var dispatcher = new FakeDispatcher();
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => new AppDbContext(_options));
        services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
        services.AddScoped<IWhatsAppNotificationDispatcher>(_ => dispatcher);
        if (chartFactory is not null)
            services.AddScoped<IWhatsAppChartFactory>(_ => chartFactory);
        var provider = services.BuildServiceProvider();

        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "WhatsApp"],
            WhatsApp = new WhatsAppChannelOptions { Phone = phone, GroupId = groupId },
        };

        var worker = new NotificationWhatsAppWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(channels),
            NullLogger<NotificationWhatsAppWorker>.Instance);

        return (context, worker, dispatcher, delivery.Id);
    }

    [Fact]
    public async Task ProcessPending_WhenSendSucceeds_ShouldMarkSentWithPhone()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent.Should().ContainSingle(m => m.DeliveryId == deliveryId);
        dispatcher.Sent[0].Phone.Should().Be("+5511999990000");
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
        delivery.LastError.Should().Be("api offline");
    }

    /// <summary>
    /// ADR-059 §3 (F33-T003): com GroupId configurado o destino é o
    /// GRUPO, mesmo existindo Phone individual (retrocompatível).
    /// </summary>
    [Fact]
    public async Task ProcessPending_WithGroupId_ShouldSendToGroupInsteadOfPhone()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync(
            phone: "+5511999990000", groupId: "120363431225988863@g.us");

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent.Should().ContainSingle(m => m.DeliveryId == deliveryId);
        dispatcher.Sent[0].Phone.Should().Be("120363431225988863@g.us");

        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Sent);
    }

    /// <summary>
    /// Config mudou entre enfileirar e despachar (GroupId e Phone
    /// esvaziados): a delivery vira Failed com erro pt-BR apontando a
    /// seção — F9-T009 cuidará do retry.
    /// </summary>
    [Fact]
    public async Task ProcessPending_WithoutGroupIdAndPhone_ShouldMarkFailedWithConfigError()
    {
        var (context, worker, dispatcher, deliveryId) = await CreateAsync(phone: "", groupId: "");

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(0);
        dispatcher.Sent.Should().BeEmpty();
        var delivery = await context.NotificationDeliveries
            .AsNoTracking().SingleAsync(d => d.Id == deliveryId);
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.LastError.Should().Contain("GroupId");
        delivery.LastError.Should().Contain("Phone");
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

    // ── F33-T005: cartão (série Zabbix) na mensagem ───────────────────

    [Fact]
    public async Task ProcessPending_WithChartFactory_ShouldAttachChartAndDeviceId()
    {
        var deviceId = Guid.NewGuid();
        var chart = new IncidentChartData(
            ChartCardKind.Incident, "Título", "sw-01", "Critical", DateTime.UtcNow,
            [new ChartSeriesPoint(DateTime.UtcNow, 99)]);
        var factory = new FakeChartFactory { Result = chart };
        var (context, worker, dispatcher, deliveryId) = await CreateAsync(
            chartFactory: factory, notificationDeviceId: deviceId);

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent[0].Chart.Should().BeSameAs(chart,
            "a fábrica devolveu dados → o message carrega o cartão");
        factory.LastDeviceId.Should().Be(deviceId,
            "o DeviceId persistido na Notification viaja até a fábrica");
    }

    [Fact]
    public async Task ProcessPending_WhenChartFactoryThrows_ShouldSendWithoutChart()
    {
        var factory = new FakeChartFactory { Throw = true };
        var (context, worker, dispatcher, _) = await CreateAsync(chartFactory: factory);

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1,
            "falha do gráfico nunca bloqueia o envio — degrada para texto");
        dispatcher.Sent.Should().ContainSingle();
        dispatcher.Sent[0].Chart.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPending_WithoutChartFactory_ShouldSendWithoutChart()
    {
        var (context, worker, dispatcher, _) = await CreateAsync();

        var processed = await worker.ProcessPendingAsync(CancellationToken.None);

        processed.Should().Be(1);
        dispatcher.Sent[0].Chart.Should().BeNull();
    }
}
