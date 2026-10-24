using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Rules.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Workers.Consumers.Notifications;

/// <summary>
/// ADR-012/docs/07: recebe os domain events do RabbitMQ, é idempotente
/// via ProcessedMessage (EventId + Consumer, docs/06) e cria a
/// Notification + seus destinatários (F9-T002) no banco. Os canais do
/// fanout vêm de Notifications:Channels:Default (F9-T006 — o admin escolhe;
/// antes era fixo SignalR+Email). ADR-062: a ativação explícita do WhatsApp
/// salva na plataforma substitui a lista de ambiente somente para este canal;
/// a checagem de destino (GroupId/Phone) usa o
/// <see cref="IWhatsAppOptionsResolver"/> (banco → env).
/// F42-T002: antes de criar a notificação, consulta a regra por EventType
/// (NotificationEventRule): Enabled=false silencia o evento; Channels
/// preenchido substitui o fanout global. Sem regra = comportamento atual.
/// </summary>
public sealed class NotificationEventConsumer(
    AppDbContext dbContext,
    IInboxStore inboxStore,
    IOptions<NotificationChannelsOptions> channelsOptions,
    IWhatsAppOptionsResolver whatsAppOptionsResolver,
    INotificationEventRuleRepository ruleRepository,
    ILogger<NotificationEventConsumer> logger)
    : IConsumer<UserCreatedDomainEvent>,
      IConsumer<UserDisabledDomainEvent>,
      IConsumer<UserUpdatedDomainEvent>,
      IConsumer<UserDeletedDomainEvent>,
      IConsumer<UserRoleAssignedDomainEvent>,
      IConsumer<UserRoleRemovedDomainEvent>,
      IConsumer<InvitationAcceptedDomainEvent>,
      IConsumer<IncidentCreatedDomainEvent>,
      IConsumer<IncidentResolvedDomainEvent>,
      IConsumer<AlertCreatedDomainEvent>,
      IConsumer<AlertResolvedDomainEvent>,
      IConsumer<PdiCreatedDomainEvent>,
      IConsumer<PdiUpdatedDomainEvent>,
      IConsumer<TaskItemCreatedDomainEvent>,
      IConsumer<TaskAssigneeCreatedDomainEvent>
{
    public NotificationEventConsumer(
        AppDbContext dbContext,
        IInboxStore inboxStore,
        IOptions<NotificationChannelsOptions> channelsOptions,
        ILogger<NotificationEventConsumer> logger)
        : this(
            dbContext,
            inboxStore,
            channelsOptions,
            new StaticWhatsAppOptionsResolver(channelsOptions.Value.WhatsApp),
            new NotificationEventRuleRepository(dbContext),
            logger)
    {
    }

    public const string ConsumerName = "NotificationConsumer";

    /// <summary>
    /// F16-T002/ADR-023: incidente (webhook Zabbix F16-T001, manual F6 ou
    /// auto-abertura F6) → destinatários por role Operator e Admin
    /// (decisão do usuário 2026-09-30).
    /// </summary>
    public Task Consume(ConsumeContext<IncidentCreatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Incidente aberto",
            $"O incidente '{context.Message.Title}' foi aberto ({context.Message.Severity}).",
            recipientUserId: null,
            recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
            context.CancellationToken,
            deviceId: context.Message.DeviceId);

    /// <summary>
    /// F19-T007: resolução do incidente (regra 7: só o InfraControl decide)
    /// → Operator + Admin.
    /// </summary>
    public Task Consume(ConsumeContext<IncidentResolvedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Incidente resolvido",
            $"O incidente '{context.Message.Title}' foi resolvido ({context.Message.Severity}).",
            recipientUserId: null,
            recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
            context.CancellationToken,
            deviceId: context.Message.DeviceId);

    /// <summary>
    /// F33-T004/ADR-059 (decisões 2/3): alerta Critical criado → Operator
    /// + Admin. A segunda mensagem ("Incidente aberto") vem do
    /// IncidentCreatedDomainEvent do incidente garantido no mesmo fluxo —
    /// as 2 mensagens da decisão 3, sem duplicar.
    /// </summary>
    public Task Consume(ConsumeContext<AlertCreatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Alerta Critical criado",
            $"O alerta '{context.Message.Message}' foi aberto ({context.Message.Severity}).",
            recipientUserId: null,
            recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
            context.CancellationToken,
            deviceId: context.Message.DeviceId);

    /// <summary>F19-T007: alerta resolvido → Operator + Admin.</summary>
    public Task Consume(ConsumeContext<AlertResolvedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Alerta resolvido",
            $"O alerta '{context.Message.Message}' foi resolvido.",
            recipientUserId: null,
            recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
            context.CancellationToken,
            deviceId: context.Message.DeviceId);

    /// <summary>
    /// PDI criado (docs/29 §8, regra do usuário 2026-10-04): notifica o
    /// DONO nos canais Email + SignalR (destaque de criação).
    /// </summary>
    public Task Consume(ConsumeContext<PdiCreatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "PDI criado",
            $"O seu PDI foi criado — prazo {context.Message.TargetDate:dd/MM/yyyy} ({StatusLabel(context.Message.Status)}).",
            recipientUserId: context.Message.UserId,
            recipientRoles: [],
            context.CancellationToken,
            channels: [NotificationChannel.Email, NotificationChannel.SignalR]);

    /// <summary>
    /// PDI atualizado (regra do usuário 2026-10-04): só SignalR. Quem
    /// mexeu ≠ dono → avisa o DONO; mexeu no próprio (operador no seu) →
    /// avisa o ADMIN (roles) — a comunicação existe nos dois sentidos.
    /// </summary>
    public Task Consume(ConsumeContext<PdiUpdatedDomainEvent> context)
    {
        var message = context.Message;
        var editedOwn = message.ActorId == message.UserId;

        return HandleAsync(
            message.EventId,
            message.EventType,
            "PDI atualizado",
            editedOwn
                ? $"O PDI de {message.OwnerEmail} foi atualizado pelo próprio dono — prazo {message.TargetDate:dd/MM/yyyy} ({StatusLabel(message.Status)})."
                : $"O PDI de {message.OwnerEmail} foi atualizado — prazo {message.TargetDate:dd/MM/yyyy} ({StatusLabel(message.Status)}).",
            recipientUserId: editedOwn ? null : message.UserId,
            recipientRoles: editedOwn ? [UserRole.Admin.ToString()] : [],
            context.CancellationToken,
            channels: [NotificationChannel.SignalR]);
    }

    /// <summary>
    /// F42-T002: tarefa criada (docs/29 §Tasks) → notifica Operator + Admin
    /// (mesmo público de incidente). Regra por evento pode silenciar ou
    /// trocar os canais.
    /// </summary>
    public Task Consume(ConsumeContext<TaskItemCreatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Tarefa criada",
            $"A tarefa '{context.Message.Title}' foi criada ({context.Message.Kind}).",
            recipientUserId: null,
            recipientRoles: [UserRole.Operator.ToString(), UserRole.Admin.ToString()],
            context.CancellationToken);

    /// <summary>
    /// F42-T002: usuário atribuído a tarefa → notifica o PRÓPRIO usuário
    /// (destinatário direto, como convite aceito).
    /// </summary>
    public Task Consume(ConsumeContext<TaskAssigneeCreatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Tarefa atribuída",
            $"Você foi atribuído à tarefa {context.Message.TaskId}.",
            recipientUserId: context.Message.UserId,
            recipientRoles: [],
            context.CancellationToken);

    private static string StatusLabel(PdiStatus status) => status switch
    {
        PdiStatus.Draft => "Rascunho",
        PdiStatus.Planned => "Planejado",
        PdiStatus.AwaitingAcceptance => "Aguardando aceite",
        PdiStatus.Active => "Ativo",
        PdiStatus.InProgress => "Em acompanhamento",
        PdiStatus.UnderEvaluation => "Em avaliação",
        PdiStatus.Completed => "Concluído",
        PdiStatus.Replanned => "Replanejado",
        PdiStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };

    public Task Consume(ConsumeContext<UserCreatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Usuário criado",
            $"O usuário {context.Message.Email} foi criado.",
            recipientUserId: context.Message.UserId,
            recipientRoles: [],
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserDisabledDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Usuário desativado",
            $"O usuário {context.Message.UserId} foi desativado.",
            recipientUserId: context.Message.UserId,
            recipientRoles: [],
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserUpdatedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Usuário atualizado",
            $"O usuário {context.Message.Email} foi atualizado ({context.Message.DisplayName}).",
            recipientUserId: context.Message.UserId,
            recipientRoles: [],
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserDeletedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Usuário excluído",
            $"O usuário {context.Message.Email} ({context.Message.DisplayName}) foi excluído.",
            recipientUserId: context.Message.UserId,
            recipientRoles: [],
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserRoleAssignedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Perfil atribuído",
            $"O perfil {context.Message.Role} foi atribuído ao usuário {context.Message.UserId}.",
            recipientUserId: null,
            recipientRoles: [context.Message.Role.ToString()],
            context.CancellationToken);

    public Task Consume(ConsumeContext<UserRoleRemovedDomainEvent> context)
        => HandleAsync(
            context.Message.EventId,
            context.Message.EventType,
            "Perfil removido",
            $"O perfil {context.Message.Role} foi removido do usuário {context.Message.UserId}.",
            recipientUserId: null,
            recipientRoles: [context.Message.Role.ToString()],
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
            recipientRoles: [],
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
    /// seu destinatário (F9-T002) na mesma transação. Destinatário é o
    /// usuário OU 1..N roles (F16-T002: incidente avisa Operator + Admin).
    /// </summary>
    public async Task HandleAsync(
        Guid eventId,
        string eventType,
        string title,
        string message,
        Guid? recipientUserId,
        string[] recipientRoles,
        CancellationToken cancellationToken,
        NotificationChannel[]? channels = null,
        Guid? deviceId = null)
    {
        // F42-T002: regra por evento — Enabled=false silencia ANTES da
        // idempotência (nada persistido); Channels preenchido substitui
        // fanout/param. Sem regra = comportamento atual (zero regressão).
        var rule = await ruleRepository.GetByEventTypeAsync(
            eventType, cancellationToken);
        if (rule is { Enabled: false })
        {
            logger.LogInformation(
                "Regra {EventType} desabilitada — notificação silenciada.",
                eventType);
            return;
        }

        var effectiveChannels = rule is { ChannelNames.Count: > 0 }
            ? rule.ChannelNames
                .Where(name => Enum.TryParse<NotificationChannel>(
                    name, ignoreCase: true, out _))
                .Select(name => Enum.Parse<NotificationChannel>(name, ignoreCase: true))
                .Distinct()
                .ToArray()
            : channels;

        // Valida antes de marcar o inbox — erro de domínio não pode virar
        // mensagem perdida (processed sem notification).
        // F33-T005: DeviceId do evento viaja até o worker de WhatsApp
        // (série histórica do gráfico no Zabbix).
        var notification = Notification.Create(eventId, eventType, title, message, deviceId);

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

        // usuário OU roles (1..N, dedup) — vazio apenas quando a resolução
        // falhou (ex.: convite sem usuário).
        if (recipientUserId is { } userId)
        {
            var recipient = NotificationRecipient.CreateForUser(notification.Id, userId);
            dbContext.NotificationRecipients.Add(recipient);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Destinatário {NotificationRecipientId} (usuário {UserId}) anexado à notificação {NotificationId}.",
                recipient.Id, userId, notification.Id);
        }
        else if (recipientRoles.Length > 0)
        {
            var roleNames = recipientRoles.Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var roleName in roleNames)
            {
                var recipient = NotificationRecipient.CreateForRole(notification.Id, roleName);
                dbContext.NotificationRecipients.Add(recipient);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Destinatário {NotificationRecipientId} (role {Role}) anexado à notificação {NotificationId}.",
                    recipient.Id, roleName, notification.Id);
            }
        }

        logger.LogInformation(
            "Notificação {NotificationId} criada a partir de {EventType}/{EventId}: {Title}.",
            notification.Id, eventType, eventId, title);

        // Canais por evento (PDI: criação Email+SignalR, edição SignalR) —
        // sem lista explícita, o fanout default de F9-T006 (Default config).
        // F42-T002: se a regra por evento definiu canais, ela substitui a
        // lista do fanout/param para ESTE evento.
        // ADR-062: destino WhatsApp (GroupId/Phone) resolvido por request
        // (banco → env) — o Grupo escolhido na UI vale sem restart.
        var whatsapp = await whatsAppOptionsResolver.ResolveAsync(cancellationToken);
        var fanout = effectiveChannels is null
            ? await ResolveFanoutChannelsAsync(whatsapp, cancellationToken)
            : effectiveChannels.Where(c => IsSendable(c, whatsapp)).Distinct().ToList();

        foreach (var channel in fanout)
            await QueueDeliveriesAsync(notification, channel, whatsapp, cancellationToken);
    }

    /// <summary>
    /// Canais do fanout = Notifications:Channels:Default (F9-T006, decisão
    /// do usuário: admin escolhe por onde enviar; default Email+SignalR).
    /// Canal desconhecido na config é ignorado com Warning; canal sem
    /// destino configurado não é enviável (também Warning) e, se NENHUM
    /// canal da lista for enviável, o fallback é Email — a notificação não
    /// morre por configuração incompleta.
    /// </summary>
    private async Task<List<NotificationChannel>> ResolveFanoutChannelsAsync(
        WhatsAppChannelOptions whatsapp, CancellationToken cancellationToken)
    {
        var configured = new List<NotificationChannel>();
        foreach (var raw in channelsOptions.Value.Default)
        {
            if (!Enum.TryParse<NotificationChannel>(raw, ignoreCase: true, out var channel))
            {
                logger.LogWarning(
                    "Canal '{Raw}' em Notifications:Channels:Default não existe — ignorado.",
                    raw);
                continue;
            }

            if (!configured.Contains(channel))
                configured.Add(channel);
        }

        if (whatsapp.NotificationsEnabled is bool whatsappEnabled)
        {
            configured.Remove(NotificationChannel.WhatsApp);
            if (whatsappEnabled)
                configured.Add(NotificationChannel.WhatsApp);
        }

        var sendable = configured.Where(c => IsSendable(c, whatsapp)).ToList();
        if (sendable.Count == 0)
        {
            logger.LogWarning(
                "Nenhum canal enviável em Notifications:Channels:Default — fallback para Email.");
            sendable.Add(NotificationChannel.Email);
        }

        await Task.CompletedTask;
        return sendable;
    }

    /// <summary>
    /// SignalR e Email não têm destino externo (sempre enviáveis). Canais
    /// com destino na config exigem a seção preenchida; canais ainda sem
    /// worker (canais futuros) não são enviáveis até a task deles.
    /// </summary>
    private bool IsSendable(NotificationChannel channel, WhatsAppChannelOptions whatsapp)
    {
        switch (channel)
        {
            case NotificationChannel.SignalR:
            case NotificationChannel.Email:
                return true;

            case NotificationChannel.Telegram:
                if (!string.IsNullOrWhiteSpace(channelsOptions.Value.Telegram.ChatId))
                    return true;
                logger.LogWarning(
                    "Canal Telegram em Notifications:Channels:Default sem ChatId configurado — deliveries não enfileiradas.");
                return false;

            case NotificationChannel.WhatsApp:
                // ADR-059 §3 (F33-T003): destino é o GroupId (broadcast no
                // grupo); Phone individual segue aceito (retrocompatível).
                // ADR-062: destino vem do resolver (banco → env).
                if (!string.IsNullOrWhiteSpace(whatsapp.GroupId)
                    || !string.IsNullOrWhiteSpace(whatsapp.Phone))
                    return true;
                logger.LogWarning(
                    "Canal WhatsApp em Notifications:Channels:Default sem GroupId nem Phone configurado — deliveries não enfileiradas.");
                return false;

            case NotificationChannel.Webhook:
                if (!string.IsNullOrWhiteSpace(channelsOptions.Value.Webhook.Url))
                    return true;
                logger.LogWarning(
                    "Canal Webhook em Notifications:Channels:Default sem Url configurada — deliveries não enfileiradas.");
                return false;

            case NotificationChannel.Slack:
                if (!string.IsNullOrWhiteSpace(channelsOptions.Value.Slack.Channel))
                    return true;
                logger.LogWarning(
                    "Canal Slack em Notifications:Channels:Default sem Channel configurado — deliveries não enfileiradas.");
                return false;

            default:
                logger.LogWarning(
                    "Canal {Channel} em Notifications:Channels:Default ainda não tem worker — deliveries não enfileiradas.",
                    channel);
                return false;
        }
    }

    /// <summary>
    /// Enfileira as deliveries do canal para cada usuário final (roles
    /// resolvidas + dedup). Preferência desligada = sem delivery (default
    /// habilitado); canal Email exige e-mail cadastrado. Quais canais
    /// entram no fanout vem de <see cref="ResolveFanoutChannels"/> (F9-T006).
    /// ADR-059 §3 (F33-T003): WhatsApp com GroupId = destino único do
    /// grupo → UMA delivery por notificação quando ≥1 destinatário
    /// habilitado; Phone individual e demais canais seguem 1 por usuário.
    /// </summary>
    private async Task QueueDeliveriesAsync(
        Notification notification, NotificationChannel channel,
        WhatsAppChannelOptions whatsapp, CancellationToken cancellationToken)
    {
        var userIds = await new NotificationRecipientRepository(dbContext)
            .ResolveUserIdsAsync(notification.Id, cancellationToken);
        if (userIds.Count == 0)
            return;

        var disabled = await dbContext.NotificationPreferences
            .Where(p => p.Channel == channel && !p.Enabled && userIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var emails = channel == NotificationChannel.Email
            ? await dbContext.DomainUsers
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken)
            : [];

        // ADR-062: destino GroupId vem do resolver (banco → env).
        var groupBroadcast = channel == NotificationChannel.WhatsApp
            && !string.IsNullOrWhiteSpace(whatsapp.GroupId);

        var added = false;
        foreach (var userId in userIds)
        {
            if (disabled.Contains(userId))
                continue;

            if (channel == NotificationChannel.Email
                && (!emails.TryGetValue(userId, out var email) || string.IsNullOrWhiteSpace(email)))
            {
                logger.LogWarning(
                    "Delivery Email não enfileirada: usuário {UserId} sem e-mail na notificação {NotificationId}.",
                    userId, notification.Id);
                continue;
            }

            dbContext.NotificationDeliveries.Add(NotificationDelivery.Create(
                notification.Id, userId, channel));
            added = true;

            // Broadcast no grupo: destino único — 1 delivery por
            // notificação (o caminho normal continua 1 por usuário).
            if (groupBroadcast)
                break;
        }

        if (added)
            await dbContext.SaveChangesAsync(cancellationToken);
    }
}
