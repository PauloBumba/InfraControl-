using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using MediatR;

namespace InfraControl.Application.Sla.TaskSlas.Queries;

// Sem cache (IQuery direto): pausas mudam a cada mutation.

public sealed record GetTaskSlaPausesQuery(Guid TaskId)
    : IQuery<IReadOnlyCollection<TaskSlaPauseDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaRead.ToString()];
}

public sealed class GetTaskSlaPausesQueryHandler(
    ITaskSlaRepository taskSlaRepository,
    ITaskSlaPauseRepository repository)
    : IRequestHandler<GetTaskSlaPausesQuery, IReadOnlyCollection<TaskSlaPauseDto>>
{
    public async Task<IReadOnlyCollection<TaskSlaPauseDto>> Handle(
        GetTaskSlaPausesQuery request, CancellationToken cancellationToken)
    {
        if (await taskSlaRepository.GetByTaskIdAsync(request.TaskId, cancellationToken) is null)
            throw new KeyNotFoundException(
                $"SLA não aplicado à task '{request.TaskId}'.");

        var pauses = await repository.GetByTaskIdAsync(request.TaskId, cancellationToken);

        return pauses
            .Select(pause => new TaskSlaPauseDto(
                pause.Id, pause.TaskId, pause.StartAt, pause.EndAt))
            .ToArray();
    }
}
