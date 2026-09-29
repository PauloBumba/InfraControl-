using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.IntegrationTests.Common;
using InfraControl.Workers.Consumers.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.IntegrationTests.Notifications;

/// <summary>
/// F9-T004: fila de deliveries SignalR ponta-a-ponta — consumer enfileira
/// (preferência respeitada) e o repositório expõe a fila do worker.
/// </summary>
public sealed class NotificationDeliveryIntegrationTests : BaseIntegrationTest
{
    private static NotificationEventConsumer CreateConsumer(AppDbContext context)
        => new(
            context,
            new InfraControl.Infrastructure.Repositories.InboxStore(context),
            Options.Create(new NotificationChannelsOptions()),
            NullLogger<NotificationEventConsumer>.Instance);

    private static async Task<Guid> SeedUserAsync(AppDbContext context, string email)
    {
        var user = User.Create(Guid.NewGuid(), email, "Usuário Delivery");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Consumer_ShouldQueueSignalRDelivery_AndRepositoryShouldExposeIt()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync(context, "delivery@exemplo.test");

        var domainEvent = new Domain.Events.UserCreatedDomainEvent(
            userId, "delivery@exemplo.test");
        await CreateConsumer(context).HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        var repository = scope.ServiceProvider
            .GetRequiredService<INotificationDeliveryRepository>();
        var pending = await repository.ListPendingByChannelAsync(
            NotificationChannel.SignalR, limit: 50, CancellationToken.None);

        pending.Should().ContainSingle(p => p.UserId == userId);
        pending.Single().Title.Should().Be("Usuário criado");
    }

    [Fact]
    public async Task Consumer_ShouldQueueEmailDeliveryWithRecipientAddress()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync(context, "email-delivery@exemplo.test");

        var domainEvent = new Domain.Events.UserCreatedDomainEvent(
            userId, "email-delivery@exemplo.test");
        await CreateConsumer(context).HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        var repository = scope.ServiceProvider
            .GetRequiredService<INotificationDeliveryRepository>();
        var pending = await repository.ListPendingByChannelAsync(
            NotificationChannel.Email, limit: 50, CancellationToken.None);

        pending.Should().ContainSingle(p => p.UserId == userId);
        pending.Single().RecipientEmail.Should().Be("email-delivery@exemplo.test");
    }

    [Fact]
    public async Task Consumer_WithSignalPreferenceDisabled_ShouldNotQueue()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync(context, "delivery-off@exemplo.test");
        context.NotificationPreferences.Add(NotificationPreference.Create(
            userId, NotificationChannel.SignalR, enabled: false));
        await context.SaveChangesAsync();

        var domainEvent = new Domain.Events.UserCreatedDomainEvent(
            userId, "delivery-off@exemplo.test");
        await CreateConsumer(context).HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", "Mensagem.",
            recipientUserId: userId, recipientRole: null,
            CancellationToken.None);

        (await context.NotificationDeliveries.AsNoTracking()
            .CountAsync(d => d.UserId == userId && d.Channel == NotificationChannel.SignalR))
            .Should().Be(0);
    }
}
