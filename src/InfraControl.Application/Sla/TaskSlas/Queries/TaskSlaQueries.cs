using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.TaskSlas.Queries;

// Sem cache (IQuery direto): SLA muda a cada aplicação.

public sealed record GetTaskSlaQuery(Guid TaskId) : IQuery<TaskSlaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskSlaQueryHandler(ITaskSlaRepository repository)
    : IRequestHandler<GetTaskSlaQuery, TaskSlaDto>
{
    public async Task<TaskSlaDto> Handle(
        GetTaskSlaQuery request, CancellationToken cancellationToken)
    {
        var sla = await repository.GetByTaskIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"SLA não aplicado à task '{request.TaskId}'.");

        return new TaskSlaDto(
            sla.TaskId, sla.SlaPolicyId,
            sla.ResponseMinutes, sla.ResolutionMinutes,
            sla.ResponseDueAt, sla.ResolutionDueAt);
    }
}
