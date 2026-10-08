using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T005/P5b: vínculo Task ↔ PDI (spec §15; alvo = plano do usuário,
// 1 por usuário — junção por user_id). Task/PDI inexistente → 404 pt-BR;
// duplicidade → 409 pt-BR.

public sealed record LinkTaskPdiCommand(
    Guid TaskId,
    Guid UserId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkTaskPdiCommandHandler(
    ITaskItemRepository taskRepository,
    IPdiRepository pdiRepository,
    ITaskPdiRepository taskPdiRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<LinkTaskPdiCommandHandler> logger)
    : IRequestHandler<LinkTaskPdiCommand>
{
    public async Task Handle(
        LinkTaskPdiCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        _ = await pdiRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");

        if (await taskPdiRepository.ExistsAsync(
                request.TaskId, request.UserId, cancellationToken))
            throw new InvalidOperationException(
                "O PDI já está vinculado à task.");

        await taskPdiRepository.AddAsync(
            TaskPdi.Create(request.TaskId, request.UserId),
            cancellationToken);
        await taskPdiRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.PdiLinked,
            request.UserId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} linked to PDI of user {UserId}.",
            request.TaskId, request.UserId);
    }
}

public sealed record UnlinkTaskPdiCommand(
    Guid TaskId,
    Guid UserId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkTaskPdiCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskPdiRepository taskPdiRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<UnlinkTaskPdiCommandHandler> logger)
    : IRequestHandler<UnlinkTaskPdiCommand>
{
    public async Task Handle(
        UnlinkTaskPdiCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var link = await taskPdiRepository.FindAsync(
                request.TaskId, request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo da task '{request.TaskId}' com o PDI do usuário "
                + $"'{request.UserId}' não encontrado.");

        link.Delete(); // ADR-030 §5 — evento de remoção na mesma transação

        taskPdiRepository.Remove(link);
        await taskPdiRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.PdiUnlinked,
            request.UserId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} unlinked from PDI of user {UserId}.",
            request.TaskId, request.UserId);
    }
}
