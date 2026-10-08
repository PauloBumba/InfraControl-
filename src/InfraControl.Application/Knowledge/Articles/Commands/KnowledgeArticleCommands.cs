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
// F24-T001/K1: tipo obrigatório (400 pt-BR), tags (limite/normalização
// no domínio) e responsável inexistente = 404; autor vem do usuário
// autenticado e é imutável.

public sealed record CreateKnowledgeArticleCommand(
    string Title,
    string Content,
    Guid? CategoryId,
    string Type,
    string? Description = null,
    IReadOnlyCollection<string>? Tags = null,
    Guid? ResponsibleId = null) : BaseCommand<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateKnowledgeArticleCommandHandler(
    IKnowledgeArticleRepository repository,
    IKnowledgeCategoryRepository categoryRepository,
    ICurrentUserContext currentUser,
    ILogger<CreateKnowledgeArticleCommandHandler> logger)
    : IRequestHandler<CreateKnowledgeArticleCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        CreateKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        await EnsureCategoryExistsAsync(
            request.CategoryId, categoryRepository, cancellationToken);
        await EnsureResponsibleExistsAsync(
            repository, request.ResponsibleId, cancellationToken);

        var article = KnowledgeArticle.Create(
            request.Title,
            request.Content,
            request.CategoryId,
            request.Type,
            request.Description,
            request.Tags,
            authorId: currentUser.UserId,
            responsibleId: request.ResponsibleId);

        await repository.AddAsync(article, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} created ({Title}).", article.Id, article.Title);

        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.CategoryId, article.Status,
            article.Type, article.Description, article.AuthorId, article.ResponsibleId)
        {
            Tags = article.Tags.Select(tag => tag.Tag).ToArray(),
        };
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

    /// <summary>
    /// Responsável precisa existir (404 pt-BR antes de persistir) —
    /// precedente F23-T001 (CreateTaskCommand).
    /// </summary>
    internal static async Task EnsureResponsibleExistsAsync(
        IKnowledgeArticleRepository repository,
        Guid? responsibleId,
        CancellationToken cancellationToken)
    {
        if (responsibleId is null)
            return;

        if (!await repository.UserExistsAsync(responsibleId.Value, cancellationToken))
            throw new KeyNotFoundException(
                $"Usuário '{responsibleId}' não encontrado.");
    }
}

public sealed record UpdateKnowledgeArticleCommand(
    Guid Id,
    string Title,
    string Content,
    Guid? CategoryId,
    string Type,
    string? Description = null,
    IReadOnlyCollection<string>? Tags = null,
    Guid? ResponsibleId = null) : BaseCommand, IAuthorizedRequest
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
        await CreateKnowledgeArticleCommandHandler.EnsureResponsibleExistsAsync(
            repository, request.ResponsibleId, cancellationToken);

        article.Update(
            request.Title,
            request.Content,
            request.CategoryId,
            request.Type,
            request.Description,
            request.Tags,
            request.ResponsibleId);
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

// F10-T003/F24-T002 (K2): transição de status — matriz de 8 estados no
// domínio (ADR-039); fora da matriz = 409, status fora do enum = 400 pt-BR.

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
        // TryParse aceita dígitos ("99") — IsDefined mantém o contrato
        // "fora do enum = 400" (K2/ADR-039 §4).
        if (!Enum.TryParse<KnowledgeArticleStatus>(
                request.Status, ignoreCase: true, out var target)
            || !Enum.IsDefined(target))
            throw new ArgumentException(
                $"Status inválido: '{request.Status}'. Use 'draft', 'inReview', " +
                "'approved', 'published', 'updated', 'archived', 'obsolete' " +
                "ou 'rejected'.",
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

        // Nomes de autor/responsável ficam com as queries Search/Get (K1).
        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.CategoryId, article.Status,
            article.Type, article.Description, article.AuthorId, article.ResponsibleId)
        {
            Tags = article.Tags.Select(tag => tag.Tag).ToArray(),
        };
    }
}
