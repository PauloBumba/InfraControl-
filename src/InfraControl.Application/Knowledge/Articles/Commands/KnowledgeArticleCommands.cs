using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Application.Knowledge.Categories.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Knowledge.Articles.Commands;

// F10-T002: CRUD de KnowledgeArticle — 400 (título/conteúdo), 404
// (artigo ou categoria de destino inexistente). Create nasce Draft.
// F10-T003: ChangeKnowledgeArticleStatus — ciclo Draft → Published →
// Archived → Draft (409 fora do ciclo), status fora do enum = 400.

public sealed record CreateKnowledgeArticleCommand(
    string Title,
    string Content,
    Guid? CategoryId) : BaseCommand<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateKnowledgeArticleCommandHandler(
    IKnowledgeArticleRepository repository,
    IKnowledgeCategoryRepository categoryRepository,
    ILogger<CreateKnowledgeArticleCommandHandler> logger)
    : IRequestHandler<CreateKnowledgeArticleCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        CreateKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        await EnsureCategoryExistsAsync(
            request.CategoryId, categoryRepository, cancellationToken);

        var article = KnowledgeArticle.Create(
            request.Title, request.Content, request.CategoryId);

        await repository.AddAsync(article, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} created ({Title}).", article.Id, article.Title);

        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.CategoryId, article.Status);
    }

    /// <summary>
    /// Categoria vem do cliente: id desconhecido = 404 pt-BR antes de
    /// persistir (FK existe, mas o erro amigável nasce aqui).
    /// </summary>
    internal static async Task EnsureCategoryExistsAsync(
        Guid? categoryId,
        IKnowledgeCategoryRepository categoryRepository,
        CancellationToken cancellationToken)
    {
        if (categoryId is null)
            return;

        var category = await categoryRepository.GetByIdAsync(
            categoryId.Value, cancellationToken);
        if (category is null)
            throw new KeyNotFoundException(
                $"Categoria '{categoryId}' não encontrada.");
    }
}

public sealed record UpdateKnowledgeArticleCommand(
    Guid Id,
    string Title,
    string Content,
    Guid? CategoryId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateKnowledgeArticleCommandHandler(
    IKnowledgeArticleRepository repository,
    IKnowledgeCategoryRepository categoryRepository,
    ILogger<UpdateKnowledgeArticleCommandHandler> logger)
    : IRequestHandler<UpdateKnowledgeArticleCommand>
{
    public async Task Handle(
        UpdateKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        await CreateKnowledgeArticleCommandHandler.EnsureCategoryExistsAsync(
            request.CategoryId, categoryRepository, cancellationToken);

        article.Update(request.Title, request.Content, request.CategoryId);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} updated ({Title}).", article.Id, article.Title);
    }
}

public sealed record DeleteKnowledgeArticleCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteKnowledgeArticleCommandHandler(
    IKnowledgeArticleRepository repository,
    ILogger<DeleteKnowledgeArticleCommandHandler> logger)
    : IRequestHandler<DeleteKnowledgeArticleCommand>
{
    public async Task Handle(
        DeleteKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        article.Delete(); // ADR-030 §5 — evento na mesma transação
        repository.Remove(article);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} deleted ({Title}).", article.Id, article.Title);
    }
}

// F10-T003: transição de status — ciclo Draft → Published → Archived →
// Draft no domínio; fora do ciclo = 409, status fora do enum = 400 pt-BR.

public sealed record ChangeKnowledgeArticleStatusCommand(
    Guid Id,
    string Status) : BaseCommand<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class ChangeKnowledgeArticleStatusCommandHandler(
    IKnowledgeArticleRepository repository,
    ILogger<ChangeKnowledgeArticleStatusCommandHandler> logger)
    : IRequestHandler<ChangeKnowledgeArticleStatusCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        ChangeKnowledgeArticleStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<KnowledgeArticleStatus>(
                request.Status, ignoreCase: true, out var target))
            throw new ArgumentException(
                $"Status inválido: '{request.Status}'. Use 'draft', 'published' ou 'archived'.",
                nameof(request.Status));

        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        // Fora do ciclo → InvalidOperationException → 409 (GlobalExceptionHandler).
        article.TransitionTo(target);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} status changed to {Status}.",
            article.Id, article.Status);

        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.CategoryId, article.Status);
    }
}
