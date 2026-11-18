using InfraControl.Application.Messaging.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Messaging.Workers;

/// <summary>Resultado de pause/resume broker-level (nunca exceção na API).</summary>
public sealed record MessagingQueueControlResult(
    bool Success,
    string? Error,
    string QueueName,
    int ConsumersCancelled,
    int ConnectionsRecycled);

/// <summary>
/// F55-T003: pausa o consumo de UMA fila no broker — cancela todos os
/// consumers ativos dela via Management API e persiste as conexões para o
/// resume reciclar (MassTransit reconecta e re-subscreve). NÃO para
/// container, NÃO apaga mensagens: o que estava unacked volta a ready.
/// </summary>
public sealed record PauseQueueConsumptionCommand(string QueueName, string? Reason)
    : BaseCommand<MessagingQueueControlResult>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;

    public string AuditResource => "MessagingQueue";

    public override IReadOnlyCollection<string> CacheKeysToInvalidate => ["messaging"];
}

public sealed class PauseQueueConsumptionCommandHandler(
    IRabbitMqManagementClient broker,
    IMessagingPauseStore pauses,
    ICurrentUserContext currentUser)
    : IRequestHandler<PauseQueueConsumptionCommand, MessagingQueueControlResult>
{
    public async Task<MessagingQueueControlResult> Handle(
        PauseQueueConsumptionCommand request, CancellationToken cancellationToken)
    {
        var queueName = request.QueueName?.Trim() ?? string.Empty;
        if (queueName.Length == 0)
        {
            return new(false, "Nome da fila é obrigatório.", queueName, 0, 0);
        }

        var consumers = await broker.GetConsumersAsync(cancellationToken);
        if (!consumers.Reachable)
        {
            return new(false, $"Broker indisponível: {consumers.UnavailableReason}",
                queueName, 0, 0);
        }

        var targets = consumers.Items
            .Where(c => c.QueueName == queueName && c.Active)
            .ToArray();
        if (targets.Length == 0)
        {
            // Já pausada ou sem consumers — idempotente, mas registra pausa
            // para o resume saber que a fila está sob gestão.
            var existing = await pauses.GetAsync(queueName, cancellationToken);
            if (existing is not null)
            {
                return new(true, "Fila já estava pausada.", queueName, 0, 0);
            }

            return new(false, "A fila não possui consumers ativos.", queueName, 0, 0);
        }

        var connectionNames = targets
            .Select(c => c.ConnectionName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct()
            .ToArray();

        var cancelled = 0;
        string? firstError = null;
        foreach (var consumer in targets)
        {
            var result = await broker.CancelConsumerAsync(
                consumer.Vhost, consumer.ConsumerTag, cancellationToken);
            if (result.Success)
            {
                cancelled++;
            }
            else
            {
                firstError ??= result.UnavailableReason ?? result.Detail;
            }
        }

        if (cancelled == 0)
        {
            return new(false,
                $"Falha ao cancelar consumers: {firstError ?? "erro desconhecido"}",
                queueName, 0, 0);
        }

        await pauses.UpsertAsync(
            MessagingQueuePause.Create(
                queueName, "/", connectionNames,
                currentUser.UserId?.ToString(), request.Reason),
            cancellationToken);

        return new(true,
            cancelled < targets.Length
                ? $"Pause parcial: {cancelled}/{targets.Length} consumers cancelados ({firstError})."
                : null,
            queueName, cancelled, connectionNames.Length);
    }
}

/// <summary>
/// F55-T003: retoma o consumo — fecha as conexões gravadas no pause; o
/// cliente RabbitMQ (MassTransit) reconecta e re-subscreve os consumers.
/// Recicla a CONEXÃO inteira: outras filas na mesma conexão também voltam
/// a consumir (limitação documentada do broker-level pause).
/// </summary>
public sealed record ResumeQueueConsumptionCommand(string QueueName)
    : BaseCommand<MessagingQueueControlResult>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;

    public string AuditResource => "MessagingQueue";

    public override IReadOnlyCollection<string> CacheKeysToInvalidate => ["messaging"];
}

public sealed class ResumeQueueConsumptionCommandHandler(
    IRabbitMqManagementClient broker,
    IMessagingPauseStore pauses)
    : IRequestHandler<ResumeQueueConsumptionCommand, MessagingQueueControlResult>
{
    public async Task<MessagingQueueControlResult> Handle(
        ResumeQueueConsumptionCommand request, CancellationToken cancellationToken)
    {
        var queueName = request.QueueName?.Trim() ?? string.Empty;
        var pause = await pauses.GetAsync(queueName, cancellationToken);
        if (pause is null)
        {
            return new(false, "A fila não está pausada.", queueName, 0, 0);
        }

        var connectionNames = pause.GetConnectionNames();
        if (connectionNames.Count == 0)
        {
            // Pausa registrada sem conexões (ex.: fila sem consumers no pause)
            // — nada a reciclar; remove o estado para não ficar zumbi.
            await pauses.DeleteAsync(queueName, cancellationToken);
            return new(true, "Nenhuma conexão para reciclar; pausa removida.",
                queueName, 0, 0);
        }

        var recycled = 0;
        string? firstError = null;
        foreach (var name in connectionNames)
        {
            var result = await broker.CloseConnectionAsync(name, cancellationToken);
            if (result.Success)
            {
                recycled++;
            }
            else
            {
                firstError ??= result.UnavailableReason ?? result.Detail;
            }
        }

        if (recycled == 0)
        {
            return new(false,
                $"Falha ao reciclar conexões: {firstError ?? "erro desconhecido"}",
                queueName, 0, 0);
        }

        await pauses.DeleteAsync(queueName, cancellationToken);

        return new(true,
            recycled < connectionNames.Count
                ? $"Resume parcial: {recycled}/{connectionNames.Count} conexões recicladas ({firstError})."
                : null,
            queueName, 0, recycled);
    }
}
