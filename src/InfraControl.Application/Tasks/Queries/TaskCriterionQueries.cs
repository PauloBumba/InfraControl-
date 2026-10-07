using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Tasks.Queries;

// F23-T003 — listagem dos critérios de aceite da Task (ordem de criação,
// mesmos guards da listagem de comentários).

public sealed record GetTaskCriteriaQuery(Guid TaskId)
    : IQuery<IReadOnlyCollection<TaskCriterionDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskCriteriaQueryHandler(
    ITaskItemRepository taskRepository,
    ITaskCriterionRepository repository)
    : IRequestHandler<GetTaskCriteriaQuery, IReadOnlyCollection<TaskCriterionDto>>
{
    public async Task<IReadOnlyCollection<TaskCriterionDto>> Handle(
        GetTaskCriteriaQuery request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var rows = await repository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);

        return rows.Select(criterion => new TaskCriterionDto(
                criterion.Id,
                criterion.TaskId,
                criterion.Description,
                criterion.IsDone,
                criterion.DoneAt,
                criterion.CreateDateTime))
            .ToArray();
    }
}
