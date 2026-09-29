using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfraControl.Workers.Consumers.Notifications;

/// <summary>
/// ADR-012/docs/07: recebe os domain events do RabbitMQ, é idempotente
/// via ProcessedMessage (EventId + Consumer, docs/06) e cria a
/// Notification + seus destinatários (F9-T002) no banco. Canais vêm em
/// F9-T004+.
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
            recipientUserId: context.Message.UserId,
            recipientRole: null,
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserDisabledDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Usuário desativado",
            $"O usuário {context.Message.UserId} foi desativado.",
            recipientUserId: context.Message.UserId,
            recipientRole: null,
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserRoleAssignedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Perfil atribuído",
            $"O perfil {context.Message.Role} foi atribuído ao usuário {context.Message.UserId}.",
            recipientUserId: null,
            recipientRole: context.Message.Role.ToString(),
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserRoleRemovedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Perfil removido",
            $"O perfil {context.Message.Role} foi removido do usuário {context.Message.UserId}.",
            recipientUserId: null,
            recipientRole: context.Message.Role.ToString(),
            context.CancellationToken);

    public Task Consume(ConsumeContext<InvitationAcceptedDomainEvent> context)
        => HandleInvitationAcceptedAsync(context.Message, context.CancellationToken);

    /// <summary>
    /// O evento carrega o e-mail; o destinatário é o usuário aceito
    /// (F9-T002). Sem usuário correspondente, a notificação fica sem
    /// destinatário direto (log Warning, não bloqueia o evento).
    /// </summary>
    public async Task HandleInvitationAcceptedAsync(
        InvitationAcceptedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdByEmailAsync(
            domainEvent.Email, cancellationToken);

        if (userId is null)
        {
            logger.LogWarning(
                "Convite aceito {InvitationId}: usuário com e-mail {Email} não encontrado — notificação sem destinatário.",
                domainEvent.InvitationId, domainEvent.Email);
        }

        await HandleAsync(
            domainEvent.EventId,
            domainEvent.EventType,
            "Convite aceito",
            $"O convite para {domainEvent.Email} foi aceito.",
            recipientUserId: userId,
            recipientRole: null,
            cancellationToken);
    }

    private async Task<Guid?> ResolveUserIdByEmailAsync(
        string email, CancellationToken cancellationToken)
        // (Guid?) é obrigatório: FirstOrDefault sobre Guid daria
        // Guid.Empty (não null) e viraria destinatário inválido.
        => await dbContext.DomainUsers
            .Where(user => user.Email.ToLower() == email.ToLower())
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Valida, marca idempotência (inbox) e persiste a notificação com
    /// seu destinatário (F9-T002) na mesma transação.
    /// </summary>
    public async Task HandleAsync(
        Guid eventId,
        string eventType,
        string title,
        string message,
        Guid? recipientUserId,
        string? recipientRole,
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

        // exatamente um destinatário (usuário OU role) — ambos nulos
        // apenas quando a resolução falhou (ex.: convite sem usuário).
        if (recipientUserId is { } userId)
        {
            var recipient = NotificationRecipient.CreateForUser(notification.Id, userId);
            dbContext.NotificationRecipients.Add(recipient);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Destinatário {NotificationRecipientId} (usuário {UserId}) anexado à notificação {NotificationId}.",
                recipient.Id, userId, notification.Id);
        }
        else if (recipientRole is { } roleName)
        {
            var recipient = NotificationRecipient.CreateForRole(notification.Id, roleName);
            dbContext.NotificationRecipients.Add(recipient);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Destinatário {NotificationRecipientId} (role {Role}) anexado à notificação {NotificationId}.",
                recipient.Id, roleName, notification.Id);
        }

        logger.LogInformation(
            "Notificação {NotificationId} criada a partir de {EventType}/{EventId}: {Title}.",
            notification.Id, eventType, eventId, title);

        await CreateSignalRDeliveriesAsync(notification, cancellationToken);
    }

    /// <summary>
    /// F9-T004: enfileira a entrega no canal SignalR para cada usuário
    /// final (roles resolvidas + dedup). Preferência desligada = sem
    /// delivery (default: habilitado). Canais futuros (F9-T005+) enfileiram
    /// os seus.
    /// </summary>
    private async Task CreateSignalRDeliveriesAsync(
        Notification notification, CancellationToken cancellationToken)
    {
        var userIds = await new NotificationRecipientRepository(dbContext)
            .ResolveUserIdsAsync(notification.Id, cancellationToken);

        var added = false;
        foreach (var userId in userIds)
        {
            var disabled = await dbContext.NotificationPreferences.AnyAsync(
                p => p.UserId == userId
                    && p.Channel == Domain.Enums.NotificationChannel.SignalR
                    && !p.Enabled,
                cancellationToken);

            if (disabled)
                continue;

            dbContext.NotificationDeliveries.Add(NotificationDelivery.Create(
                notification.Id, userId, Domain.Enums.NotificationChannel.SignalR));
            added = true;
        }

        if (added)
            await dbContext.SaveChangesAsync(cancellationToken);
    }
}
