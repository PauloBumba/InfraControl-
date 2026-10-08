using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F23-T005/P5a: vínculo Task ↔ KnowledgeArticle (docs/32 §10, spec Tasks
// §16) — task/artigo inexistente → 404 pt-BR; duplicidade → 409 pt-BR.

public sealed record LinkTaskArticleCommand(
    Guid TaskId,
    Guid ArticleId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkTaskArticleCommandHandler(
    ITaskItemRepository taskRepository,
    IKnowledgeArticleRepository articleRepository,
    ITaskKnowledgeArticleRepository taskKnowledgeRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<LinkTaskArticleCommandHandler> logger)
    : IRequestHandler<LinkTaskArticleCommand>
{
    public async Task Handle(
        LinkTaskArticleCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        if (await taskKnowledgeRepository.ExistsAsync(
                request.TaskId, request.ArticleId, cancellationToken))
            throw new InvalidOperationException(
                "O artigo já está vinculado à task.");

        await taskKnowledgeRepository.AddAsync(
            TaskKnowledgeArticle.Create(request.TaskId, request.ArticleId),
            cancellationToken);
        await taskKnowledgeRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.KnowledgeLinked,
            request.ArticleId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} linked to knowledge article {ArticleId}.",
            request.TaskId, request.ArticleId);
    }
}

public sealed record UnlinkTaskArticleCommand(
    Guid TaskId,
    Guid ArticleId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkTaskArticleCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskKnowledgeArticleRepository taskKnowledgeRepository,
    ITaskHistoryWriter historyWriter,
    ILogger<UnlinkTaskArticleCommandHandler> logger)
    : IRequestHandler<UnlinkTaskArticleCommand>
{
    public async Task Handle(
        UnlinkTaskArticleCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var link = await taskKnowledgeRepository.FindAsync(
                request.TaskId, request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo da task '{request.TaskId}' com o artigo "
                + $"'{request.ArticleId}' não encontrado.");

        link.Delete(); // ADR-030 §5 — junçãoDeletedDomainEvent na mesma transação

        taskKnowledgeRepository.Remove(link);
        await taskKnowledgeRepository.SaveChangesAsync(cancellationToken);

        await historyWriter.RecordAsync(
            request.TaskId, TaskHistoryAction.KnowledgeUnlinked,
            request.ArticleId.ToString(), cancellationToken);

        logger.LogInformation(
            "Task {TaskId} unlinked from knowledge article {ArticleId}.",
            request.TaskId, request.ArticleId);
    }
}
