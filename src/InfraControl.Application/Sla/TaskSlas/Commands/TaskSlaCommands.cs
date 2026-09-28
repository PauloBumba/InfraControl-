using InfraControl.Application.Sla.Policies.Ports;
using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Sla.TaskSlas.Commands;

// F8-T003: aplicação de SLA à task — snapshot dos prazos (docs/04);
// 404 (task/política), 409 (já aplicado / exclusão de política com tasks).

public sealed record ApplyTaskSlaCommand(
    Guid TaskId,
    Guid SlaPolicyId) : BaseCommand<TaskSlaDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class ApplyTaskSlaCommandHandler(
    Tasks.Ports.ITaskItemRepository taskRepository,
    ISlaPolicyRepository policyRepository,
    ITaskSlaRepository repository,
    ITaskHistoryWriter historyWriter,
    ILogger<ApplyTaskSlaCommandHandler> logger)
    : IRequestHandler<ApplyTaskSlaCommand, TaskSlaDto>
{
    public async Task<TaskSlaDto> Handle(
        ApplyTaskSlaCommand request, CancellationToken cancellationToken)
    {
        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task '{request.TaskId}' não encontrada.");

        var policy = await policyRepository.GetByIdAsync(request.SlaPolicyId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Política de SLA '{request.SlaPolicyId}' não encontrada.");

        if (await repository.GetByTaskIdAsync(task.Id, cancellationToken) is not null)
            throw new InvalidOperationException("Já existe um SLA aplicado a esta task.");

        var sla = Domain.Entities.TaskSla.Apply(task, policy);
        await repository.AddAsync(sla, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            task.Id, TaskHistoryAction.SlaApplied,
            $"resposta {policy.ResponseMinutes} min, resolução {policy.ResolutionMinutes} min",
            cancellationToken);

        logger.LogInformation(
            "SLA {SlaPolicyId} applied to task {TaskId} (due {ResponseDueAt}/{ResolutionDueAt}).",
            policy.Id, task.Id, sla.ResponseDueAt, sla.ResolutionDueAt);

        return new TaskSlaDto(
            sla.TaskId, sla.SlaPolicyId,
            sla.ResponseMinutes, sla.ResolutionMinutes,
            sla.ResponseDueAt, sla.ResolutionDueAt);
    }
}
