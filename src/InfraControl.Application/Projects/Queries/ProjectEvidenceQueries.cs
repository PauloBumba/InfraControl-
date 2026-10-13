using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Projects.Queries;

// CRUD-Projetos (2026-10-12) — leitura de evidências técnicas do Projeto:
// listagem por projeto (read-model da UI) e download do binário (o stream é
// devolvido ao controller, que responde com o content-type armazenado +
// nosniff — mesma semântica do `DownloadPdiEvidenceQuery`).

public sealed record GetProjectEvidenceQuery(Guid ProjectId)
    : IQuery<ProjectEvidenceDto[]>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetProjectEvidenceQueryHandler(
    IProjectEvidenceRepository evidenceRepository)
    : IRequestHandler<GetProjectEvidenceQuery, ProjectEvidenceDto[]>
{
    public async Task<ProjectEvidenceDto[]> Handle(
        GetProjectEvidenceQuery request, CancellationToken cancellationToken)
    {
        var evidences = await evidenceRepository.ListByProjectIdAsync(
            request.ProjectId, cancellationToken);

        return evidences
            .Select(UploadProjectEvidenceCommandHandler.ToDto)
            .ToArray();
    }
}

/// <summary>Conteúdo do arquivo para o controller escrever na resposta.</summary>
public sealed record ProjectEvidenceDownload(
    string FileName,
    string ContentType,
    Stream Content);

public sealed record DownloadProjectEvidenceQuery(Guid Id)
    : IQuery<ProjectEvidenceDownload>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class DownloadProjectEvidenceQueryHandler(
    IProjectEvidenceRepository evidenceRepository,
    IProjectEvidenceFileStore fileStore)
    : IRequestHandler<DownloadProjectEvidenceQuery, ProjectEvidenceDownload>
{
    public async Task<ProjectEvidenceDownload> Handle(
        DownloadProjectEvidenceQuery request, CancellationToken cancellationToken)
    {
        var evidence = await evidenceRepository.GetByIdAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Evidência '{request.Id}' não encontrada.");

        var content = await fileStore.OpenReadAsync(
            evidence.Id, evidence.Extension, cancellationToken);

        return new ProjectEvidenceDownload(
            evidence.OriginalFileName, evidence.ContentType, content);
    }
}
