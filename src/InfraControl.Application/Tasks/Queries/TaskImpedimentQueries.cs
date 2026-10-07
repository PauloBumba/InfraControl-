using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Tasks.Queries;

// F23-T004/P4b — listagem dos impedimentos estruturados da Task (ordem de
// criação, mesmos guards da listagem de critérios).

public sealed record GetTaskImpedimentsQuery(Guid TaskId)
    : IQuery<IReadOnlyCollection<TaskImpedimentDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskImpedimentsQueryHandler(
    ITaskItemRepository taskRepository,
    ITaskImpedimentRepository repository)
    : IRequestHandler<GetTaskImpedimentsQuery, IReadOnlyCollection<TaskImpedimentDto>>
{
    public async Task<IReadOnlyCollection<TaskImpedimentDto>> Handle(
        GetTaskImpedimentsQuery request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var rows = await repository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);

        return rows.Select(impediment => new TaskImpedimentDto(
                impediment.Id,
                impediment.TaskId,
                impediment.Description,
                impediment.CreateDateTime,
                impediment.ResolvedAt))
            .ToArray();
    }
}
