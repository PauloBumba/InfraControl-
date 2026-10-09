using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Pdi.Queries;

// Download da evidência (docs/30 §12, Fase 4 — ADR-034): mesma regra
// de leitura do acompanhamento (Admin/dono/gestor); o stream é devolvido
// ao controller, que responde com o content-type armazenado + nosniff.

/// <summary>Conteúdo do arquivo para o controller escrever na resposta.</summary>
public sealed record PdiEvidenceDownload(
    string FileName,
    string ContentType,
    Stream Content);

public sealed record DownloadPdiEvidenceQuery(Guid Id)
    : IQuery<PdiEvidenceDownload>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DownloadPdiEvidenceQueryHandler(
    IPdiStructureRepository repository,
    IPdiRepository plans,
    ICurrentUserContext currentUser,
    IPdiEvidenceFileStore fileStore)
    : IRequestHandler<DownloadPdiEvidenceQuery, PdiEvidenceDownload>
{
    public async Task<PdiEvidenceDownload> Handle(
        DownloadPdiEvidenceQuery request, CancellationToken cancellationToken)
    {
        var evidence = await repository.GetEvidenceAsync(
                request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Evidência '{request.Id}' não encontrada.");

        var plan = await PdiStructureLookup.PlanForPhaseAsync(
            repository, plans, evidence.PhaseId, cancellationToken);
        PdiAccess.EnsureCanView(currentUser, plan);

        var content = await fileStore.OpenReadAsync(
            evidence.Id, evidence.Extension, cancellationToken);

        return new PdiEvidenceDownload(
            evidence.OriginalFileName, evidence.ContentType, content);
    }
}
