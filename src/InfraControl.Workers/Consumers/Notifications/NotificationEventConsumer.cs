using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers.Consumers.Notifications;

/// <summary>
/// ADR-012/docs/07: recebe os domain events do RabbitMQ, é idempotente
/// via ProcessedMessage (EventId + Consumer, docs/06) e cria a
/// Notification no banco. Destinatários/canais vêm em F9-T002+.
/// </summary>
public sealed class NotificationEventConsumer(
    AppDbContext dbContext,
    IInboxStore inboxStore,
    ILogger<NotificationEventConsumer> logger)
    : IConsumer<UserCreatedDomainEvent>,
      IConsumer<UserDisabledDomainEvent>,
      IConsumer<UserRoleAssignedDomainEvent>,
      IConsumer<UserRoleRemovedDomainEvent>,
      IConsumer<InvitationAcceptedDomainEvent>
{
    public const string ConsumerName = "NotificationConsumer";

    public Task Consume(ConsumeContext<UserCreatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Usuário criado",
            $"O usuário {context.Message.Email} foi criado.",
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserDisabledDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Usuário desativado",
            $"O usuário {context.Message.UserId} foi desativado.",
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserRoleAssignedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Perfil atribuído",
            $"O perfil {context.Message.Role} foi atribuído ao usuário {context.Message.UserId}.",
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserRoleRemovedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Perfil removido",
            $"O perfil {context.Message.Role} foi removido do usuário {context.Message.UserId}.",
            context.CancellationToken);

    public Task Consume(ConsumeContext<InvitationAcceptedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Convite aceito",
            $"O convite para {context.Message.Email} foi aceito.",
            context.CancellationToken);

    /// <summary>Valida, marca idempotência (inbox) e persiste a notificação.</summary>
    public async Task HandleAsync(
        Guid eventId,
        string eventType,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        // Valida antes de marcar o inbox — erro de domínio não pode virar
        // mensagem perdida (processed sem notification).
        var notification = Notification.Create(eventId, eventType, title, message);

        var firstDelivery = await inboxStore.TryMarkProcessedAsync(
            eventId, ConsumerName, cancellationToken);
        if (!firstDelivery)
        {
            logger.LogInformation(
                "Evento {EventType}/{EventId} já processado — notificação duplicada ignorada.",
                eventType, eventId);
            return;
        }

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Notificação {NotificationId} criada a partir de {EventType}/{EventId}: {Title}.",
            notification.Id, eventType, eventId, title);
    }
}
