using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Messaging.Explorer;
using InfraControl.Application.Messaging.Ports;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Messaging.Failures;

/// <summary>
/// F55-T005: Retry + DLQ — deliveries Failed separadas em "retry agendado"
/// (NextAttemptAt futuro) e "DLQ lógica" (tentativas esgotadas, sem
/// próxima tentativa). Mesmos filtros do explorer de deliveries.
/// </summary>
public sealed record SearchMessagingRetriesQuery(
    Guid? DeliveryId, Guid? NotificationId, string? Channel,
    int? MinAttempts, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize)
    : BaseQuery<PagedResult<MessagingDeliveryItemDto>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SearchMessagingRetriesQueryHandler(IMessagingExplorerStore store)
    : IRequestHandler<SearchMessagingRetriesQuery, PagedResult<MessagingDeliveryItemDto>>
{
    public async Task<PagedResult<MessagingDeliveryItemDto>> Handle(
        SearchMessagingRetriesQuery request, CancellationToken cancellationToken)
    {
        var result = await store.SearchFailureDeliveriesAsync(new MessagingDeliveryFilter(
            request.DeliveryId, request.NotificationId, request.Channel, null,
            request.MinAttempts, request.FromUtc, request.ToUtc,
            request.Page, request.PageSize), deadLetter: false, cancellationToken);

        return new PagedResult<MessagingDeliveryItemDto>(
            result.Items.Select(MessagingFailureMapping.ToDto).ToArray(),
            result.TotalCount, result.Page, result.PageSize);
    }
}

public sealed record SearchMessagingDlqQuery(
    Guid? DeliveryId, Guid? NotificationId, string? Channel,
    int? MinAttempts, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize)
    : BaseQuery<PagedResult<MessagingDeliveryItemDto>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SearchMessagingDlqQueryHandler(IMessagingExplorerStore store)
    : IRequestHandler<SearchMessagingDlqQuery, PagedResult<MessagingDeliveryItemDto>>
{
    public async Task<PagedResult<MessagingDeliveryItemDto>> Handle(
        SearchMessagingDlqQuery request, CancellationToken cancellationToken)
    {
        var result = await store.SearchFailureDeliveriesAsync(new MessagingDeliveryFilter(
            request.DeliveryId, request.NotificationId, request.Channel, null,
            request.MinAttempts, request.FromUtc, request.ToUtc,
            request.Page, request.PageSize), deadLetter: true, cancellationToken);

        return new PagedResult<MessagingDeliveryItemDto>(
            result.Items.Select(MessagingFailureMapping.ToDto).ToArray(),
            result.TotalCount, result.Page, result.PageSize);
    }
}

internal static class MessagingFailureMapping
{
    public static MessagingDeliveryItemDto ToDto(Domain.Entities.NotificationDelivery d)
        => new(d.Id, d.NotificationId, d.UserId, d.Channel.ToString(),
            d.Status.ToString(), d.AttemptCount, d.NextAttemptAt, d.SentAt,
            d.LastError, d.SuppressionReason, d.CreateDateTime);
}

public sealed record ReplayDeliveryResult(
    bool Success, string? Error, Guid DeliveryId, string Status);

/// <summary>
/// F55-T005: replay seletivo e idempotente — devolve a MESMA delivery à
/// fila (Pending + NextAttemptAt=agora); o worker do canal reenvia com a
/// idempotência já existente. Exige Administer + auditoria porque pode
/// gerar efeito externo (WhatsApp/e-mail). Nunca cria delivery nova,
/// nunca apaga histórico (AttemptCount/LastError preservados).
/// </summary>
public sealed record ReplayNotificationDeliveryCommand(
    Guid DeliveryId, string? Reason)
    : BaseCommand<ReplayDeliveryResult>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;

    public string AuditResource => "NotificationDelivery";

    public override IReadOnlyCollection<string> CacheKeysToInvalidate => ["messaging"];
}

public sealed class ReplayNotificationDeliveryCommandHandler(
    INotificationDeliveryRepository deliveries,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReplayNotificationDeliveryCommand, ReplayDeliveryResult>
{
    public async Task<ReplayDeliveryResult> Handle(
        ReplayNotificationDeliveryCommand request, CancellationToken cancellationToken)
    {
        var delivery = await deliveries.GetByIdAsync(request.DeliveryId, cancellationToken);
        if (delivery is null)
        {
            return new(false, "Delivery não encontrada.", request.DeliveryId, "missing");
        }

        if (delivery.Status != DeliveryStatus.Failed)
        {
            return new(false,
                $"Apenas deliveries Failed podem ser reprocessadas (estado: {delivery.Status}).",
                delivery.Id, delivery.Status.ToString());
        }

        delivery.RequeueForReplay(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new(true, null, delivery.Id, delivery.Status.ToString());
    }
}
