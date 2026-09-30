using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Workers.Consumers.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// ADR-012/docs/07: o Notification Consumer recebe o evento do RabbitMQ,
/// é idempotente via ProcessedMessage (EventId + Consumer, docs/06) e cria
/// a Notification no PostgreSQL.
/// </summary>
public sealed class NotificationEventConsumerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationEventConsumerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// F9-T006: os canais do fanout vêm de `Notifications:Channels:Default`
    /// (default = Email + SignalR, igual ao antigo comportamento fixo).
    /// </summary>
    private async Task<(AppDbContext Context, NotificationEventConsumer Consumer)> CreateAsync(
        NotificationChannelsOptions? channels = null)
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var consumer = new NotificationEventConsumer(
            context,
            new InboxStore(context),
            Options.Create(channels ?? new NotificationChannelsOptions()),
            NullLogger<NotificationEventConsumer>.Instance);
        return (context, consumer);
    }

    /// <summary>Delivery tem FK p/ DomainUsers — o fluxo real sempre tem o usuário.</summary>
    private static async Task SeedUserAsync(AppDbContext context, Guid userId, string email)
    {
        context.DomainUsers.Add(User.Create(userId, email, "Usuário"));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Consume_UserCreated_ShouldCreateNotificationWithUserRecipient()
    {
        var (context, consumer) = await CreateAsync();
        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "user@example.com");
        await SeedUserAsync(context, domainEvent.UserId, domainEvent.Email);

        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", $"O usuário {domainEvent.Email} foi criado.",
            recipientUserId: domainEvent.UserId, recipientRole: null,
            CancellationToken.None);

        var notifications = await context.Notifications.AsNoTracking().ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].EventId.Should().Be(domainEvent.EventId);
        notifications[0].EventType.Should().Be("UserCreatedDomainEvent");
        notifications[0].Title.Should().Be("Usuário criado");
        notifications[0].Message.Should().Contain("user@example.com");

        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.NotificationId.Should().Be(notifications[0].Id);
        recipient.UserId.Should().Be(domainEvent.UserId);
        recipient.Role.Should().BeNull();
    }

    [Fact]
    public async Task Consume_SameEventTwice_ShouldNotDuplicate()
    {
        var (context, consumer) = await CreateAsync();
        var domainEvent = new UserDisabledDomainEvent(Guid.NewGuid());
        await SeedUserAsync(context, domainEvent.UserId, "desativado@exemplo.test");

        var handle = () => consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário desativado", $"O usuário {domainEvent.UserId} foi desativado.",
            recipientUserId: domainEvent.UserId, recipientRole: null,
            CancellationToken.None);

        await handle();
        await handle();

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.ProcessedMessages.AsNoTracking().CountAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task Consume_RoleAssigned_ShouldCarryRoleInMessage()
    {
        var (context, consumer) = await CreateAsync();
        var userId = Guid.NewGuid();
        var domainEvent = new UserRoleAssignedDomainEvent(userId, UserRole.Viewer);

        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Perfil atribuído", $"O perfil {domainEvent.Role} foi atribuído ao usuário {domainEvent.UserId}.",
            recipientUserId: null, recipientRole: domainEvent.Role.ToString(),
            CancellationToken.None);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.Message.Should().Contain("Viewer");
        notification.Message.Should().Contain(userId.ToString());

        // F9-T002: evento de role → destinatário é a ROLE, não o usuário.
        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.Role.Should().Be("Viewer");
        recipient.UserId.Should().BeNull();
    }

    [Fact]
    public async Task Consume_UserCreated_ShouldQueueSignalRDelivery()
    {
        var (context, consumer) = await CreateAsync();
        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "fila@exemplo.test");
        await SeedUserAsync(context, domainEvent.UserId, domainEvent.Email);

        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", $"O usuário {domainEvent.Email} foi criado.",
            recipientUserId: domainEvent.UserId, recipientRole: null,
            CancellationToken.None);

        var delivery = await context.NotificationDeliveries.AsNoTracking()
            .SingleAsync(d => d.Channel == NotificationChannel.SignalR);
        delivery.UserId.Should().Be(domainEvent.UserId);
        delivery.Status.Should().Be(DeliveryStatus.Pending);
    }

    [Fact]
    public async Task Consume_WithSignalRPreferenceDisabled_ShouldNotQueueDelivery()
    {
        var (context, consumer) = await CreateAsync();
        var userId = Guid.NewGuid();
        context.DomainUsers.Add(User.Create(userId, "desligado@exemplo.test", "Desligado"));
        context.NotificationPreferences.Add(NotificationPreference.Create(
            userId, NotificationChannel.SignalR, enabled: false));
        await context.SaveChangesAsync();

        var domainEvent = new UserCreatedDomainEvent(userId, "desligado@exemplo.test");
        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", "Mensagem.",
            recipientUserId: domainEvent.UserId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.SignalR)).Should().Be(0);
    }

    [Fact]
    public async Task Consume_UserCreated_ShouldQueueEmailDeliveryWithAddress()
    {
        var (context, consumer) = await CreateAsync();
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "fila-email@exemplo.test");

        var domainEvent = new UserCreatedDomainEvent(userId, "fila-email@exemplo.test");
        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        var delivery = await context.NotificationDeliveries.AsNoTracking()
            .SingleAsync(d => d.Channel == NotificationChannel.Email);
        delivery.UserId.Should().Be(userId);
        delivery.Status.Should().Be(DeliveryStatus.Pending);
    }

    [Fact]
    public async Task Consume_WithEmailPreferenceDisabled_ShouldQueueOnlySignalR()
    {
        var (context, consumer) = await CreateAsync();
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "email-off@exemplo.test");
        context.NotificationPreferences.Add(NotificationPreference.Create(
            userId, NotificationChannel.Email, enabled: false));
        await context.SaveChangesAsync();

        var domainEvent = new UserCreatedDomainEvent(userId, "email-off@exemplo.test");
        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(0);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.SignalR)).Should().Be(1);
    }

    [Fact]
    public async Task Consume_InvitationAccepted_ShouldResolveUserByEmail()
    {
        var (context, consumer) = await CreateAsync();
        var user = User.Create(Guid.NewGuid(), "convite@exemplo.test", "Convite");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();

        var domainEvent = new InvitationAcceptedDomainEvent(
            Guid.NewGuid(), "convite@exemplo.test");

        await consumer.HandleInvitationAcceptedAsync(domainEvent, CancellationToken.None);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.Message.Should().Contain("convite@exemplo.test");

        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Consume_InvitationAccepted_UnknownEmail_ShouldCreateWithoutRecipient()
    {
        var (context, consumer) = await CreateAsync();
        var domainEvent = new InvitationAcceptedDomainEvent(
            Guid.NewGuid(), "desconhecido@exemplo.test");

        await consumer.HandleInvitationAcceptedAsync(domainEvent, CancellationToken.None);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(0);
    }

    /// <summary>Telegram na lista Default com ChatId = delivery Telegram enfileirada.</summary>
    [Fact]
    public async Task Consume_WithTelegramInDefault_ShouldQueueTelegramDelivery()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Telegram"],
            Telegram = new TelegramChannelOptions { ChatId = "-100555" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "telegram@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        var delivery = await context.NotificationDeliveries.AsNoTracking()
            .SingleAsync(d => d.Channel == NotificationChannel.Telegram);
        delivery.UserId.Should().Be(userId);
        delivery.Status.Should().Be(DeliveryStatus.Pending);
    }

    /// <summary>Preferência desligada no canal = sem delivery (regra F9-T003 vale para todo canal).</summary>
    [Fact]
    public async Task Consume_WithTelegramPreferenceDisabled_ShouldNotQueueTelegramDelivery()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Telegram"],
            Telegram = new TelegramChannelOptions { ChatId = "-100555" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        context.DomainUsers.Add(User.Create(userId, "telegram-off@exemplo.test", "Desligado"));
        context.NotificationPreferences.Add(NotificationPreference.Create(
            userId, NotificationChannel.Telegram, enabled: false));
        await context.SaveChangesAsync();

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Telegram)).Should().Be(0);
    }

    /// <summary>Telegram na lista mas SEM destino = pula (Warning) e os canais seguem.</summary>
    [Fact]
    public async Task Consume_WithTelegramUnconfigured_ShouldSkipTelegramAndQueueDefaults()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Telegram"],
            Telegram = new TelegramChannelOptions { ChatId = "" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "sem-telegram@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Telegram)).Should().Be(0);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.SignalR)).Should().Be(1);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(1);
    }

    /// <summary>
    /// Fallback de e-mail: NENHUM canal da lista é enviável (Default = só
    /// Telegram sem ChatId) → enfileira Email — a notificação não morre por
    /// configuração incompleta.
    /// </summary>
    [Fact]
    public async Task Consume_WithOnlyUnsendableChannels_ShouldFallbackToEmail()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Telegram"],
            Telegram = new TelegramChannelOptions { ChatId = " " },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "fallback@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(1);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Telegram)).Should().Be(0);
    }

    /// <summary>
    /// Fanout é CONFIG-driven (F9-T006): canal fora da lista Default não
    /// recebe delivery (antes SignalR+Email eram fixos) e canal inexistente
    /// na config (ex.: "Slack", F9-T010) é ignorado sem quebrar.
    /// </summary>
    [Fact]
    public async Task Consume_WithConfiguredChannels_ShouldFanoutExactlyTheDefaultList()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "Slack"],
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "só-email@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(1);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.SignalR)).Should().Be(0);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Telegram)).Should().Be(0);
    }

    /// <summary>WhatsApp na lista Default com Phone = delivery WhatsApp enfileirada (F9-T007).</summary>
    [Fact]
    public async Task Consume_WithWhatsAppInDefault_ShouldQueueWhatsAppDelivery()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "WhatsApp"],
            WhatsApp = new WhatsAppChannelOptions { Phone = "+5511888887777" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "whatsapp@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        var delivery = await context.NotificationDeliveries.AsNoTracking()
            .SingleAsync(d => d.Channel == NotificationChannel.WhatsApp);
        delivery.UserId.Should().Be(userId);
        delivery.Status.Should().Be(DeliveryStatus.Pending);
    }

    /// <summary>Preferência WhatsApp desligada = sem delivery (regra F9-T003).</summary>
    [Fact]
    public async Task Consume_WithWhatsAppPreferenceDisabled_ShouldNotQueueWhatsAppDelivery()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "WhatsApp"],
            WhatsApp = new WhatsAppChannelOptions { Phone = "+5511888887777" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        context.DomainUsers.Add(User.Create(userId, "whatsapp-off@exemplo.test", "Desligado"));
        context.NotificationPreferences.Add(NotificationPreference.Create(
            userId, NotificationChannel.WhatsApp, enabled: false));
        await context.SaveChangesAsync();

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.WhatsApp)).Should().Be(0);
    }

    /// <summary>WhatsApp na lista mas SEM destino = pula (Warning) e os canais seguem.</summary>
    [Fact]
    public async Task Consume_WithWhatsAppUnconfigured_ShouldSkipWhatsAppAndQueueDefaults()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "WhatsApp"],
            WhatsApp = new WhatsAppChannelOptions { Phone = "" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "sem-whatsapp@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.WhatsApp)).Should().Be(0);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.SignalR)).Should().Be(1);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(1);
    }

    /// <summary>
    /// Fallback de e-mail genérico (F9-T006, vale para qualquer canal):
    /// Default = só WhatsApp sem Phone → enfileira Email.
    /// </summary>
    [Fact]
    public async Task Consume_WithOnlyUnconfiguredWhatsApp_ShouldFallbackToEmail()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["WhatsApp"],
            WhatsApp = new WhatsAppChannelOptions { Phone = "  " },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "fallback-whatsapp@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(1);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.WhatsApp)).Should().Be(0);
    }

    /// <summary>Webhook na lista Default com Url = delivery Webhook enfileirada (F9-T008).</summary>
    [Fact]
    public async Task Consume_WithWebhookInDefault_ShouldQueueWebhookDelivery()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Webhook"],
            Webhook = new WebhookChannelOptions { Url = "https://exemplo.test/webhook" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "com-webhook@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        await context.NotificationDeliveries.AsNoTracking()
            .SingleAsync(d => d.Channel == NotificationChannel.Webhook);
    }

    /// <summary>Preferência Webhook desligada = sem delivery (regra F9-T003).</summary>
    [Fact]
    public async Task Consume_WithWebhookPreferenceDisabled_ShouldNotQueueWebhookDelivery()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Webhook"],
            Webhook = new WebhookChannelOptions { Url = "https://exemplo.test/webhook" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "webhook-off@exemplo.test");
        context.NotificationPreferences.Add(NotificationPreference.Create(
            userId, NotificationChannel.Webhook, enabled: false));
        await context.SaveChangesAsync();

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Webhook)).Should().Be(0);
    }

    /// <summary>Webhook na lista mas SEM Url = pula (Warning) e os canais seguem.</summary>
    [Fact]
    public async Task Consume_WithWebhookUnconfigured_ShouldSkipWebhookAndQueueDefaults()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Email", "SignalR", "Webhook"],
            Webhook = new WebhookChannelOptions { Url = "" },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "sem-webhook@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Webhook)).Should().Be(0);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.SignalR)).Should().Be(1);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(1);
    }

    /// <summary>Fallback de e-mail: Default = só Webhook sem Url → enfileira Email.</summary>
    [Fact]
    public async Task Consume_WithOnlyUnconfiguredWebhook_ShouldFallbackToEmail()
    {
        var channels = new NotificationChannelsOptions
        {
            Default = ["Webhook"],
            Webhook = new WebhookChannelOptions { Url = "  " },
        };
        var (context, consumer) = await CreateAsync(channels);
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId, "fallback-webhook@exemplo.test");

        await consumer.HandleAsync(
            Guid.NewGuid(), "UserCreatedDomainEvent",
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Email)).Should().Be(1);
        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.Channel == NotificationChannel.Webhook)).Should().Be(0);
    }
}
