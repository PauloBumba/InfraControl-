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
<<<<<<< HEAD
// F10-T003 + F24-T002/K2: ChangeKnowledgeArticleStatus — workflow de 8
// status (ADR-034; 409 fora da matriz, status fora do enum = 400).
// F24-T003/K3: toda versão registra o motivo (spec docs/32 §8).
=======
// F10-T003: ChangeKnowledgeArticleStatus — ciclo Draft → Published →
// Archived → Draft (409 fora do ciclo), status fora do enum = 400.
// F24-T001/K1: tipo obrigatório (400 pt-BR), tags (limite/normalização
// no domínio) e responsável inexistente = 404; autor vem do usuário
// autenticado e é imutável.
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

public sealed record CreateKnowledgeArticleCommand(
    string Title,
    string Content,
    Guid? CategoryId,
<<<<<<< HEAD
    // F24-T001/K1 — identificação (docs/32 §6); opcional para não
    // quebrar os chamadores antigos.
    string? Type = null,
=======
    string Type,
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
<<<<<<< HEAD
    IKnowledgeArticleVersionRepository versionRepository,
=======
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    ICurrentUserContext currentUser,
    ILogger<CreateKnowledgeArticleCommandHandler> logger)
    : IRequestHandler<CreateKnowledgeArticleCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        CreateKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var type = ParseType(request.Type);

        await EnsureCategoryExistsAsync(
            request.CategoryId, categoryRepository, cancellationToken);
        await EnsureResponsibleExistsAsync(
            repository, request.ResponsibleId, cancellationToken);

        var article = KnowledgeArticle.Create(
<<<<<<< HEAD
            request.Title, request.Content, request.CategoryId,
            type, request.Description, request.Tags, request.ResponsibleId,
            currentUser.UserId);
=======
            request.Title,
            request.Content,
            request.CategoryId,
            request.Type,
            request.Description,
            request.Tags,
            authorId: currentUser.UserId,
            responsibleId: request.ResponsibleId);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

        await repository.AddAsync(article, cancellationToken);

        // F24-T003/K3 (docs/32 §8): a criação é a v1.0 — snapshot com
        // os 5 campos da spec (alteração/autor/data/motivo/aprovador;
        // aprovador vem na aprovação do K2).
        await versionRepository.AddAsync(
            KnowledgeArticleVersion.SnapshotOf(
                article, "Criação do artigo", currentUser.UserId),
            cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} created ({Title}).", article.Id, article.Title);

<<<<<<< HEAD
        return await KnowledgeArticleDto.FromAsync(
            article, repository, cancellationToken);
    }

    /// <summary>
    /// Tipo da spec docs/32 §5 (F24-T001/K1): null/vazio = artigo sem
    /// tipo (a spec não define default); fora da lista = 400 pt-BR com
    /// as opções (molde do status F10-T003).
    /// </summary>
    internal static KnowledgeArticleType? ParseType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        if (Enum.TryParse<KnowledgeArticleType>(type, ignoreCase: true, out var parsed))
            return parsed;

        throw new ArgumentException(
            $"Tipo inválido: '{type}'. Use 'procedure', 'manual', 'guide', 'faq', "
            + "'knownsolution', 'configuration', 'technicaldocumentation', "
            + "'bestpractices', 'policy', 'internalstandard', 'troubleshooting', "
            + "'systemknowledge', 'infrastructureknowledge' ou 'operacionalknowledge'.",
            nameof(type));
=======
        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.CategoryId, article.Status,
            article.Type, article.Description, article.AuthorId, article.ResponsibleId)
        {
            Tags = article.Tags.Select(tag => tag.Tag).ToArray(),
        };
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
<<<<<<< HEAD
    /// Responsável do artigo (F24-T001/K1): id desconhecido = 404
    /// pt-BR — mesma frase do responsável de Task.
=======
    /// Responsável precisa existir (404 pt-BR antes de persistir) —
    /// precedente F23-T001 (CreateTaskCommand).
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
<<<<<<< HEAD
    // F24-T003/K3 — motivo obrigatório (spec docs/32 §8: toda versão
    // registra o motivo); vem antes dos opcionais da K1 (C# exige
    // opcionais no fim).
    string Reason,
    // F24-T001/K1 — identificação (docs/32 §6).
    string? Type = null,
=======
    string Type,
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
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
    IKnowledgeArticleVersionRepository versionRepository,
    ICurrentUserContext currentUser,
    ILogger<UpdateKnowledgeArticleCommandHandler> logger)
    : IRequestHandler<UpdateKnowledgeArticleCommand>
{
    public async Task Handle(
        UpdateKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var type = CreateKnowledgeArticleCommandHandler.ParseType(request.Type);

        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        await CreateKnowledgeArticleCommandHandler.EnsureCategoryExistsAsync(
            request.CategoryId, categoryRepository, cancellationToken);
        await CreateKnowledgeArticleCommandHandler.EnsureResponsibleExistsAsync(
            repository, request.ResponsibleId, cancellationToken);

<<<<<<< HEAD
        article.Update(request.Title, request.Content, request.CategoryId);
        // Autor imutável (K1): SetIdentification não toca AuthorId.
        article.SetIdentification(
            type, request.Description, request.Tags, request.ResponsibleId);

        // F24-T005/K5 (docs/32 §12): "atualizar" satisfaz a revisão →
        // reagenda a próxima (no-op quando não há intervalo).
        article.PostponeReview();

        // F24-T003/K3 (docs/32 §8): edição = nova versão (minor, ou
        // major novo se houve aprovação) + snapshot imutável com o
        // motivo — SnapshotOf valida motivo vazio/longo em pt-BR (400).
        article.BumpVersion();
        await versionRepository.AddAsync(
            KnowledgeArticleVersion.SnapshotOf(
                article, request.Reason, currentUser.UserId),
            cancellationToken);

=======
        article.Update(
            request.Title,
            request.Content,
            request.CategoryId,
            request.Type,
            request.Description,
            request.Tags,
            request.ResponsibleId);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} updated ({Title} → {Version}).",
            article.Id, article.Title, article.Version);
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
    IKnowledgeArticleVersionRepository versionRepository,
    ICurrentUserContext currentUser,
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
<<<<<<< HEAD
                $"Status inválido: '{request.Status}'. Use 'draft', 'inreview', "
                + "'approved', 'published', 'updated', 'archived', 'obsolete' ou 'rejected'.",
=======
                $"Status inválido: '{request.Status}'. Use 'draft', 'inReview', " +
                "'approved', 'published', 'updated', 'archived', 'obsolete' " +
                "ou 'rejected'.",
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
                nameof(request.Status));

        var article = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.Id}' não encontrado.");

        // Fora do ciclo → InvalidOperationException → 409 (GlobalExceptionHandler).
        article.TransitionTo(target);

        // F24-T003/K3 + K2/ADR-034: aprovar carimba o aprovador na
        // versão corrente (só o primeiro vale) e põe o piso para a
        // próxima edição abrir major novo (1.2 → 2.0).
        if (target == KnowledgeArticleStatus.Approved)
        {
            article.MarkVersionApproved();
            var current = await versionRepository.GetLatestAsync(
                article.Id, cancellationToken);
            current?.Approve(currentUser.UserId);
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} status changed to {Status}.",
            article.Id, article.Status);

<<<<<<< HEAD
        return await KnowledgeArticleDto.FromAsync(
            article, repository, cancellationToken);
=======
        // Nomes de autor/responsável ficam com as queries Search/Get (K1).
        return new KnowledgeArticleDto(
            article.Id, article.Title, article.Content, article.CategoryId, article.Status,
            article.Type, article.Description, article.AuthorId, article.ResponsibleId)
        {
            Tags = article.Tags.Select(tag => tag.Tag).ToArray(),
        };
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    }
}
