using FluentAssertions;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.Application.Knowledge.Categories.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Knowledge;

/// <summary>
/// F10-T002: KnowledgeArticle ponta-a-ponta — CRUD com categoria, 404
/// (artigo e categoria), 400 (título) e 403 sem role de escrita.
/// </summary>
public sealed class KnowledgeArticleIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task ArticleCrud_ShouldPersist()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var category = await mediator.Send(new CreateKnowledgeCategoryCommand("Redes", null));

        var created = await mediator.Send(new CreateKnowledgeArticleCommand(
            "Troca do switch central", "Failover passo a passo.", category.Id, "Procedimento"));
        created.Title.Should().Be("Troca do switch central");
        created.Status.Should().Be(KnowledgeArticleStatus.Draft);

        await mediator.Send(new UpdateKnowledgeArticleCommand(
<<<<<<< HEAD
            created.Id, "Troca do switch central (v2)", "Conteúdo novo.", null,
            "Ajuste do roteiro."));
=======
            created.Id, "Troca do switch central (v2)", "Conteúdo novo.", null, "Procedimento"));
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

        var fetched = await mediator.Send(new GetKnowledgeArticleQuery(created.Id));
        fetched.Title.Should().Be("Troca do switch central (v2)");
        fetched.CategoryId.Should().BeNull();

        var page = await mediator.Send(new SearchKnowledgeArticlesQuery("switch"));
        page.Items.Should().ContainSingle(article => article.Id == created.Id);

        await mediator.Send(new DeleteKnowledgeArticleCommand(created.Id));
        (await mediator.Send(new SearchKnowledgeArticlesQuery())).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UnknownCategory_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", unknown, "Guia"));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Get_UnknownArticle_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new GetKnowledgeArticleQuery(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Create_EmptyTitle_ShouldFailWith400PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var action = () => mediator.Send(
            new CreateKnowledgeArticleCommand("  ", "Conteúdo.", null, "Guia"));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O título do artigo é obrigatório.*");
    }

    [Fact]
    public async Task Create_WithoutOperateRole_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new CreateKnowledgeArticleCommand("Sem permissão", "Conteúdo.", null, "Guia"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

<<<<<<< HEAD
    /// <summary>Cadeia completa via API: Draft → InReview → Approved →
    /// Published → Archived → Draft (workflow F24-T002/K2, ADR-034).</summary>
=======
    /// <summary>Ciclo editorial do §7 via API: Rascunho → Em revisão →
    /// Aprovada → Publicada → Arquivada → Rascunho.</summary>
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    [Fact]
    public async Task ChangeStatus_Lifecycle_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var created = await mediator.Send(
            new CreateKnowledgeArticleCommand("Artigo de publicação", "Conteúdo.", null, "Manual"));

        var inReview = await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "inReview"));
        inReview.Status.Should().Be(KnowledgeArticleStatus.InReview);

        var approved = await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "approved"));
        approved.Status.Should().Be(KnowledgeArticleStatus.Approved);

        var inReview = await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "inreview"));
        inReview.Status.Should().Be(KnowledgeArticleStatus.InReview);

        var approved = await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "approved"));
        approved.Status.Should().Be(KnowledgeArticleStatus.Approved);

        var published = await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "published"));
        published.Status.Should().Be(KnowledgeArticleStatus.Published);

        var archived = await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "archived"));
        archived.Status.Should().Be(KnowledgeArticleStatus.Archived);

        var restored = await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "draft"));
        restored.Status.Should().Be(KnowledgeArticleStatus.Draft);

        var fetched = await mediator.Send(new GetKnowledgeArticleQuery(created.Id));
        fetched.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_ShouldFailWith409PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var created = await mediator.Send(
            new CreateKnowledgeArticleCommand("Rascunho", "Conteúdo.", null, "FAQ"));

        var action = () => mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(created.Id, "archived"));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Transição inválida: 'Rascunho' → 'Arquivada'.");
    }

    [Fact]
    public async Task Draft_ShouldBeHiddenFromViewer()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var draft = await mediator.Send(
            new CreateKnowledgeArticleCommand("Rascunho interno", "Segredo.", null, "FAQ"));
        var inReview = await mediator.Send(
            new CreateKnowledgeArticleCommand("Em revisão", "Não publicado.", null, "FAQ"));
        await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(inReview.Id, "inReview"));
        var published = await mediator.Send(
            new CreateKnowledgeArticleCommand("Artigo público", "Aberto.", null, "FAQ"));
        await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(published.Id, "inReview"));
        await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(published.Id, "approved"));
        await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(published.Id, "inreview"));
        await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(published.Id, "approved"));
        await mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(published.Id, "published"));

        UseRole(UserRole.Viewer);
        var page = await mediator.Send(new SearchKnowledgeArticlesQuery());
        page.Items.Should().ContainSingle(article => article.Id == published.Id);

        var getInReview = () => mediator.Send(new GetKnowledgeArticleQuery(inReview.Id));
        (await getInReview.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{inReview.Id}' não encontrado.");

        var getDraft = () => mediator.Send(new GetKnowledgeArticleQuery(draft.Id));
        (await getDraft.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{draft.Id}' não encontrado.");

        var change = () => mediator.Send(
            new ChangeKnowledgeArticleStatusCommand(draft.Id, "published"));
        (await change.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    // ── F24-T001: tipos e identificação (K1) ─────────────────────────────

    [Fact]
    public async Task Create_WithIdentification_ShouldPersist()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var created = await mediator.Send(new CreateKnowledgeArticleCommand(
            "Guia de failover", "Passo a passo.", null,
            Type: "Guia",
            Description: "Guia de failover do switch central.",
            Tags: ["Redes", "Switch"],
            ResponsibleId: null));

        created.Type.Should().Be("Guia");
        created.Description.Should().Be("Guia de failover do switch central.");
        created.Tags.Should().Equal("redes", "switch");

        var fetched = await mediator.Send(new GetKnowledgeArticleQuery(created.Id));
        fetched.Type.Should().Be("Guia");
        fetched.Tags.Should().Equal("redes", "switch");
    }

    [Fact]
    public async Task Create_UnknownResponsible_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new CreateKnowledgeArticleCommand(
            "Artigo", "Conteúdo.", null, "Guia", ResponsibleId: unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Create_UnknownType_ShouldFailWith400PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var action = () => mediator.Send(
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", null, "Receita"));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Tipo inválido: 'Receita'. Use:*");
    }
}
