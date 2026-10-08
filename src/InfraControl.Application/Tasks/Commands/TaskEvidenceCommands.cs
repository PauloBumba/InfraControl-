using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T004/P4c — evidências da Task (Plano de Execução §11): upload em
// disco local (ADR-033) com metadados na tabela. 404 pt-BR para
// task/evidência inexistente, 400 do domínio para nome/extensão/tamanho
// fora do permitido (allowlist + 10 MB), um registro de histórico por
// operação (EvidenceAdded/Removed) com "nome (kB)" truncado para o
// limit de 200 do TaskHistory. O arquivo sai do disco junto com o
// registro na exclusão (erro de IO não bloqueia).

public sealed record UploadTaskEvidenceCommand(
    Guid TaskId,
    string FileName,
    string? ContentType,
    byte[] Content,
    string? Note = null) : BaseCommand<TaskEvidenceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UploadTaskEvidenceCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskEvidenceRepository evidenceRepository,
    IEvidenceFileStore fileStore,
    ITaskHistoryWriter historyWriter,
    ILogger<UploadTaskEvidenceCommandHandler> logger)
    : IRequestHandler<UploadTaskEvidenceCommand, TaskEvidenceDto>
{
    public async Task<TaskEvidenceDto> Handle(
        UploadTaskEvidenceCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        // Domínio valida allowlist/10 MB/nome (ADR-033) e gera o
        // StoredName (GUID + extensão) — nunca o caminho do usuário.
        var evidence = TaskEvidence.Create(
            request.TaskId,
            request.FileName,
            request.ContentType,
            request.Content.Length,
            request.Note);

        await fileStore.SaveAsync(
            evidence.StoredName, request.Content, cancellationToken);

        await evidenceRepository.AddAsync(evidence, cancellationToken);
        await evidenceRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            evidence.TaskId,
            TaskHistoryAction.EvidenceAdded,
            FormatDetail(evidence),
            cancellationToken);

        logger.LogInformation(
            "Evidence {EvidenceId} uploaded to task {TaskId} ({FileSize} bytes).",
            evidence.Id, evidence.TaskId, evidence.FileSize);

        return ToDto(evidence);
    }

    internal static TaskEvidenceDto ToDto(TaskEvidence evidence) =>
        new(
            evidence.Id,
            evidence.TaskId,
            evidence.FileName,
            evidence.ContentType,
            evidence.FileSize,
            evidence.Note,
            evidence.CreateDateTime);

    /// <summary>"nome.ext (123 kB)" — nome truncado em 150 → detail ≤200.</summary>
    internal static string FormatDetail(TaskEvidence evidence)
    {
        var kilobytes = Math.Max(1, (long)Math.Ceiling(evidence.FileSize / 1024d));
        return $"{TaskPlanningFormats.Truncate(evidence.FileName, 150)} ({kilobytes} kB)";
    }
}

public sealed record DeleteTaskEvidenceCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteTaskEvidenceCommandHandler(
    ITaskEvidenceRepository evidenceRepository,
    IEvidenceFileStore fileStore,
    ITaskHistoryWriter historyWriter,
    ILogger<DeleteTaskEvidenceCommandHandler> logger)
    : IRequestHandler<DeleteTaskEvidenceCommand>
{
    public async Task Handle(
        DeleteTaskEvidenceCommand request, CancellationToken cancellationToken)
    {
        var evidence = await evidenceRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Evidência '{request.Id}' não encontrada.");

        // Histórico antes do Remove — o registro sobrevive à exclusão.
        await historyWriter.RecordAsync(
            evidence.TaskId,
            TaskHistoryAction.EvidenceRemoved,
            UploadTaskEvidenceCommandHandler.FormatDetail(evidence),
            cancellationToken);

        evidence.Delete(); // ADR-030 §5 — TaskEvidenceDeletedDomainEvent
        evidenceRepository.Remove(evidence);
        await evidenceRepository.SaveChangesAsync(cancellationToken);

        // Arquivo sai depois do banco (banco = fonte de verdade); o
        // store loga e engole erro de IO em vez de falhar a request.
        fileStore.Delete(evidence.StoredName);

        logger.LogInformation(
            "Evidence {EvidenceId} deleted from task {TaskId}.",
            evidence.Id, evidence.TaskId);
    }
}
