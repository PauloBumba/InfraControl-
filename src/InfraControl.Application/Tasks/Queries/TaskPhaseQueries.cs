using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Tasks.Queries;

// F23-T004/P4d — listagem das fases da Task (ordem de posição; gaps de
// posição após exclusão são aceitos — a ordenação por `Position` é estável).

public sealed record GetTaskPhasesQuery(Guid TaskId)
    : IQuery<IReadOnlyCollection<TaskPhaseDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskPhasesQueryHandler(
    ITaskItemRepository taskRepository,
    ITaskPhaseRepository repository)
    : IRequestHandler<GetTaskPhasesQuery, IReadOnlyCollection<TaskPhaseDto>>
{
    public async Task<IReadOnlyCollection<TaskPhaseDto>> Handle(
        GetTaskPhasesQuery request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var rows = await repository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);

        return rows.Select(AddTaskPhaseCommandHandler.ToDto).ToArray();
    }
}
