using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Projects.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Knowledge.Articles.Commands;

// F24-T004/K4 (spec docs/32 §9): origem do artigo — múltiplas
// origens polimórficas (Task/PDI/Chamado), artigo-centro. O vínculo
// de USO continua no F10-T004/P5a ("esta task usa este artigo") —
// origem é "de onde veio". 400 (tipo) / 404 (artigo ou alvo) /
// 409 (duplicado), mesmo molde dos vínculos de device.

public sealed record LinkKnowledgeArticleOriginCommand(
    Guid ArticleId,
    string Type,
    Guid OriginId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkKnowledgeArticleOriginCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleOriginRepository originRepository,
    ITaskItemRepository taskRepository,
    IPdiRepository pdiRepository,
    IProjectRepository projectRepository,
    IIncidentRepository incidentRepository,
    ILogger<LinkKnowledgeArticleOriginCommandHandler> logger)
    : IRequestHandler<LinkKnowledgeArticleOriginCommand>
{
    public async Task Handle(
        LinkKnowledgeArticleOriginCommand request, CancellationToken cancellationToken)
    {
        var originType = ParseType(request.Type);

        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        await EnsureOriginExistsAsync(
            originType, request.OriginId, cancellationToken);

        if (await originRepository.ExistsAsync(
                request.ArticleId, originType, request.OriginId, cancellationToken))
            throw new InvalidOperationException(
                "A origem já está vinculada ao artigo.");

        await originRepository.AddAsync(
            KnowledgeArticleOrigin.Create(
                request.ArticleId, originType, request.OriginId),
            cancellationToken);
        await originRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} origin linked ({OriginType} {OriginId}).",
            request.ArticleId, originType, request.OriginId);
    }

    internal static KnowledgeOriginType ParseType(string type)
        => Enum.TryParse<KnowledgeOriginType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"Origem inválida: '{type}'. Use 'task', 'pdi', 'project' ou 'incident'.",
                nameof(type));

    private async Task EnsureOriginExistsAsync(
        KnowledgeOriginType originType,
        Guid originId,
        CancellationToken cancellationToken)
    {
        var exists = originType switch
        {
            KnowledgeOriginType.Task =>
                await taskRepository.GetByIdAsync(originId, cancellationToken) is not null,
            KnowledgeOriginType.Pdi =>
                await pdiRepository.GetByIdAsync(originId, cancellationToken) is not null,
            KnowledgeOriginType.Project =>
                await projectRepository.GetByIdAsync(originId, cancellationToken) is not null,
            KnowledgeOriginType.Incident =>
                await incidentRepository.GetByIdAsync(originId, cancellationToken) is not null,
            _ => false,
        };

        if (!exists)
            throw new KeyNotFoundException(originType switch
            {
                KnowledgeOriginType.Task => $"Task '{originId}' não encontrada.",
                KnowledgeOriginType.Pdi => $"PDI '{originId}' não encontrado.",
                KnowledgeOriginType.Project => $"Projeto '{originId}' não encontrado.",
                _ => $"Incidente '{originId}' não encontrado.",
            });
    }
}

public sealed record UnlinkKnowledgeArticleOriginCommand(
    Guid ArticleId,
    string Type,
    Guid OriginId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkKnowledgeArticleOriginCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleOriginRepository originRepository,
    ILogger<UnlinkKnowledgeArticleOriginCommandHandler> logger)
    : IRequestHandler<UnlinkKnowledgeArticleOriginCommand>
{
    public async Task Handle(
        UnlinkKnowledgeArticleOriginCommand request, CancellationToken cancellationToken)
    {
        var originType =
            LinkKnowledgeArticleOriginCommandHandler.ParseType(request.Type);

        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        var origin = await originRepository.FindAsync(
                request.ArticleId, originType, request.OriginId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Origem '{request.Type.ToLowerInvariant()}' '{request.OriginId}' "
                + $"não encontrada para o artigo '{request.ArticleId}'.");

        origin.Delete(); // ADR-030 §5 — evento na mesma transação
        originRepository.Remove(origin);
        await originRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} origin unlinked ({OriginType} {OriginId}).",
            request.ArticleId, originType, request.OriginId);
    }
}
