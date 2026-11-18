using InfraControl.Application.Messaging.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Messaging.Explorer;

/// <summary>
/// F55-T002: Queue/Exchange/Binding/Connection Explorer — leitura nível
/// Operate via Management API (backend-only). O envelope de disponibilidade
/// (`Reachable`/`UnavailableReason`) é parte do contrato da UI: broker fora
/// mostra o motivo, nunca lista vazia mascarando indisponibilidade.
/// </summary>
public sealed record GetMessagingQueuesQuery
    : BaseQuery<RabbitMqListResult<RabbitMqQueueSummary>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingQueuesQueryHandler(IRabbitMqManagementClient broker)
    : IRequestHandler<GetMessagingQueuesQuery, RabbitMqListResult<RabbitMqQueueSummary>>
{
    public Task<RabbitMqListResult<RabbitMqQueueSummary>> Handle(
        GetMessagingQueuesQuery request, CancellationToken cancellationToken)
        => broker.GetQueuesAsync(cancellationToken);
}

public sealed record GetMessagingQueueDetailQuery(string Name)
    : BaseQuery<RabbitMqDetailResult<RabbitMqQueueDetail>>, IAuthorizedRequest
{
    public override string CacheKey => $"{GetType().Name}:{Name}";

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingQueueDetailQueryHandler(IRabbitMqManagementClient broker)
    : IRequestHandler<GetMessagingQueueDetailQuery, RabbitMqDetailResult<RabbitMqQueueDetail>>
{
    public Task<RabbitMqDetailResult<RabbitMqQueueDetail>> Handle(
        GetMessagingQueueDetailQuery request, CancellationToken cancellationToken)
        => broker.GetQueueAsync(request.Name, cancellationToken);
}

public sealed record GetMessagingExchangesQuery
    : BaseQuery<RabbitMqListResult<RabbitMqExchangeSummary>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingExchangesQueryHandler(IRabbitMqManagementClient broker)
    : IRequestHandler<GetMessagingExchangesQuery, RabbitMqListResult<RabbitMqExchangeSummary>>
{
    public Task<RabbitMqListResult<RabbitMqExchangeSummary>> Handle(
        GetMessagingExchangesQuery request, CancellationToken cancellationToken)
        => broker.GetExchangesAsync(cancellationToken);
}

public sealed record GetMessagingBindingsQuery
    : BaseQuery<RabbitMqListResult<RabbitMqBindingSummary>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingBindingsQueryHandler(IRabbitMqManagementClient broker)
    : IRequestHandler<GetMessagingBindingsQuery, RabbitMqListResult<RabbitMqBindingSummary>>
{
    public Task<RabbitMqListResult<RabbitMqBindingSummary>> Handle(
        GetMessagingBindingsQuery request, CancellationToken cancellationToken)
        => broker.GetBindingsAsync(cancellationToken);
}

public sealed record GetMessagingConnectionsQuery
    : BaseQuery<RabbitMqListResult<RabbitMqConnectionSummary>>, IAuthorizedRequest
{
    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(10);

    public override IReadOnlyCollection<string> CacheTags => ["messaging"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetMessagingConnectionsQueryHandler(IRabbitMqManagementClient broker)
    : IRequestHandler<GetMessagingConnectionsQuery, RabbitMqListResult<RabbitMqConnectionSummary>>
{
    public Task<RabbitMqListResult<RabbitMqConnectionSummary>> Handle(
        GetMessagingConnectionsQuery request, CancellationToken cancellationToken)
        => broker.GetConnectionsAsync(cancellationToken);
}
