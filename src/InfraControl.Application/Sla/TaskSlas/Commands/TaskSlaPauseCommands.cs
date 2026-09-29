using InfraControl.Application.Sla.TaskSlas.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Sla.TaskSlas.Commands;

// F8-T004: pausas do relógio de SLA — 404 (sem SLA), 409 (pausa aberta
// duplicada / resume sem pausa); ao fechar, a duração soma os *DueAt
// (snapshot de minutos intacto — docs/04).

public sealed record PauseTaskSlaCommand(Guid TaskId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class PauseTaskSlaCommandHandler(
    ITaskSlaRepository taskSlaRepository,
    ITaskSlaPauseRepository repository,
    ITaskHistoryWriter historyWriter,
    ILogger<PauseTaskSlaCommandHandler> logger)
    : IRequestHandler<PauseTaskSlaCommand>
{
    public async Task Handle(
        PauseTaskSlaCommand request, CancellationToken cancellationToken)
    {
        if (await taskSlaRepository.GetByTaskIdAsync(request.TaskId, cancellationToken) is null)
            throw new KeyNotFoundException(
                $"SLA não aplicado à task '{request.TaskId}'.");

        if (await repository.GetOpenByTaskIdAsync(request.TaskId, cancellationToken) is not null)
            throw new InvalidOperationException(
                "Já existe uma pausa em aberto para o SLA desta task.");

        var pause = Domain.Entities.TaskSlaPause.Start(request.TaskId);
        await repository.AddAsync(pause, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.SlaPaused,
            $"início {pause.StartAt:O}", cancellationToken);

        logger.LogInformation(
            "SLA pause started for task {TaskId} at {StartAt}.",
            request.TaskId, pause.StartAt);
    }
}

public sealed record ResumeTaskSlaCommand(Guid TaskId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class ResumeTaskSlaCommandHandler(
    ITaskSlaRepository taskSlaRepository,
    ITaskSlaPauseRepository repository,
    ITaskHistoryWriter historyWriter,
    ILogger<ResumeTaskSlaCommandHandler> logger)
    : IRequestHandler<ResumeTaskSlaCommand>
{
    public async Task Handle(
        ResumeTaskSlaCommand request, CancellationToken cancellationToken)
    {
        var sla = await taskSlaRepository.GetByTaskIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"SLA não aplicado à task '{request.TaskId}'.");

        var pause = await repository.GetOpenByTaskIdAsync(request.TaskId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Não existe pausa em aberto para o SLA desta task.");

        pause.Close();
        sla.ExtendDueDates(pause.Duration);

        await repository.SaveChangesAsync(cancellationToken); // audit: pause + TaskSla rastreado?

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.SlaResumed,
            $"+{(int)pause.Duration.TotalMinutes} min nos prazos", cancellationToken);

        logger.LogInformation(
            "SLA pause ended for task {TaskId} ({Duration} min — due extended).",
            request.TaskId, (int)pause.Duration.TotalMinutes);
    }
}
