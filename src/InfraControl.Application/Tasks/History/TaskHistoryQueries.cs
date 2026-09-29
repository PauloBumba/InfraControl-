using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Tasks.History;

// F7-T006: leitura do histórico — ordem cronológica com ator; task
// inexistente → 404 pt-BR (consistência com GetTask).

public sealed record GetTaskHistoryQuery(Guid TaskId)
    : IQuery<IReadOnlyCollection<TaskHistoryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskHistoryQueryHandler(
    ITaskHistoryRepository repository,
    ITaskItemRepository taskRepository)
    : IRequestHandler<GetTaskHistoryQuery, IReadOnlyCollection<TaskHistoryDto>>
{
    public async Task<IReadOnlyCollection<TaskHistoryDto>> Handle(
        GetTaskHistoryQuery request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var rows = await repository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);

        return rows.Select(row => new TaskHistoryDto(
                row.Entry.Id,
                row.Entry.TaskId,
                row.Entry.ActorId,
                row.ActorName,
                row.Entry.Action.ToString(),
                row.Entry.Detail,
                row.Entry.CreateDateTime))
            .ToArray();
    }
}
