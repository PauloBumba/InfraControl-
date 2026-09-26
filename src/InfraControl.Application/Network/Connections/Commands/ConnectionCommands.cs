using InfraControl.Application.Inventory.NetworkInterfaces.Queries;
using InfraControl.Application.Network.Connections.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Network.Connections.Commands;

// F3-T005: CRUD de Connection (Interface N:N Interface via Connection,
// docs/03). Par DIRIGIDO; só self-loop proibido; mesmo device permitido
// (decisões do usuário). Mapeamento: ArgumentException → 400,
// KeyNotFoundException → 404, InvalidOperationException → 409.

public sealed record CreateConnectionCommand(
    Guid SourceInterfaceId,
    Guid TargetInterfaceId) : BaseCommand<ConnectionDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class CreateConnectionCommandHandler(
    IConnectionRepository repository,
    GetNetworkInterfaceQueryHandler interfaceQueryHandler,
    ILogger<CreateConnectionCommandHandler> logger)
    : IRequestHandler<CreateConnectionCommand, ConnectionDto>
{
    public async Task<ConnectionDto> Handle(
        CreateConnectionCommand request,
        CancellationToken cancellationToken)
    {
        // Self-loop antes dos guards: 400 mesmo se o id não existir.
        if (request.SourceInterfaceId == request.TargetInterfaceId)
            throw new ArgumentException(
                "A conexão não pode ligar a interface a ela mesma.",
                nameof(request.TargetInterfaceId));

        // Guards 404 pt-BR reusados do slice F2-T004.
        var source = await interfaceQueryHandler.Handle(
            new GetNetworkInterfaceQuery(request.SourceInterfaceId), cancellationToken);
        var target = await interfaceQueryHandler.Handle(
            new GetNetworkInterfaceQuery(request.TargetInterfaceId), cancellationToken);

        var existing = await repository.GetPairAsync(
            request.SourceInterfaceId, request.TargetInterfaceId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("Já existe uma conexão entre essas interfaces.");

        var connection = Connection.Create(
            request.SourceInterfaceId, request.TargetInterfaceId);
        await repository.AddAsync(connection, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Connection {Source} → {Target} created.",
            request.SourceInterfaceId, request.TargetInterfaceId);

        return new ConnectionDto(
            connection.Id,
            source.Id, source.Name, source.DeviceId,
            target.Id, target.Name, target.DeviceId);
    }
}

public sealed record DeleteConnectionCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class DeleteConnectionCommandHandler(
    IConnectionRepository repository,
    ILogger<DeleteConnectionCommandHandler> logger)
    : IRequestHandler<DeleteConnectionCommand>
{
    public async Task Handle(
        DeleteConnectionCommand request,
        CancellationToken cancellationToken)
    {
        var connection = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Conexão '{request.Id}' não encontrada.");

        repository.Remove(connection);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Connection {ConnectionId} deleted.", connection.Id);
    }
}
