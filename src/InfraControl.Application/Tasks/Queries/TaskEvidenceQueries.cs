using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Tasks.Queries;

// F23-T004/P4c — listagem das evidências da Task (ordem de upload) e
// download do binário (mesmos guards das demais listagens; o arquivo
// ausente do disco vira 404 pt-BR, como a linha inexistente).

public sealed record GetTaskEvidenceQuery(Guid TaskId)
    : IQuery<IReadOnlyCollection<TaskEvidenceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskEvidenceQueryHandler(
    ITaskItemRepository taskRepository,
    ITaskEvidenceRepository repository)
    : IRequestHandler<GetTaskEvidenceQuery, IReadOnlyCollection<TaskEvidenceDto>>
{
    public async Task<IReadOnlyCollection<TaskEvidenceDto>> Handle(
        GetTaskEvidenceQuery request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var rows = await repository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);

        return rows.Select(evidence => new TaskEvidenceDto(
                evidence.Id,
                evidence.TaskId,
                evidence.FileName,
                evidence.ContentType,
                evidence.FileSize,
                evidence.Note,
                evidence.CreateDateTime))
            .ToArray();
    }
}

public sealed record GetTaskEvidenceDownloadQuery(Guid Id)
    : IQuery<TaskEvidenceDownloadDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed record TaskEvidenceDownloadDto(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed class GetTaskEvidenceDownloadQueryHandler(
    ITaskEvidenceRepository repository,
    IEvidenceFileStore fileStore)
    : IRequestHandler<GetTaskEvidenceDownloadQuery, TaskEvidenceDownloadDto>
{
    public async Task<TaskEvidenceDownloadDto> Handle(
        GetTaskEvidenceDownloadQuery request, CancellationToken cancellationToken)
    {
        var evidence = await repository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Evidência '{request.Id}' não encontrada.");

        var content = await fileStore.ReadAsync(
            evidence.StoredName, cancellationToken);

        return new TaskEvidenceDownloadDto(
            evidence.FileName, evidence.ContentType, content);
    }
}
