using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T002: KnowledgeArticle — invariantes pt-BR (docs/04: artigos da
/// base de conhecimento; estados Draft/Published/Archived — as
/// transições de status vêm em F10-T003).
/// </summary>
public sealed class KnowledgeArticleTests
{
    [Fact]
    public void Create_ShouldPersistTitleContentAndCategory()
    {
        var categoryId = Guid.NewGuid();

        var article = KnowledgeArticle.Create(
            "Como trocar o switch central",
            "Passo a passo da troca com failover.",
            categoryId);

        article.Title.Should().Be("Como trocar o switch central");
        article.Content.Should().Be("Passo a passo da troca com failover.");
        article.CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public void Create_ShouldStartAsDraft()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);

        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void Create_WithoutCategory_ShouldAcceptNull()
    {
        var article = KnowledgeArticle.Create("Sem categoria", "Conteúdo.", null);

        article.CategoryId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimTitle()
    {
        var article = KnowledgeArticle.Create("  Roteiro  ", "Conteúdo.", null);

        article.Title.Should().Be("Roteiro");
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("   ", "Conteúdo.", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O título do artigo é obrigatório.*");
    }

    [Fact]
    public void Create_TitleOver200_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create(new string('a', 201), "Conteúdo.", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O título do artigo pode ter no máximo 200 caracteres.*");
    }

    [Fact]
    public void Create_EmptyContent_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("Roteiro", "   ", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O conteúdo do artigo é obrigatório.*");
    }

    [Fact]
    public void Create_ContentOver20000_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("Roteiro", new string('c', 20_001), null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O conteúdo do artigo pode ter no máximo 20000 caracteres.*");
    }

    /// <summary>Update não mexe no Status — transições são F10-T003.</summary>
    [Fact]
    public void Update_ShouldChangeTitleContentCategory_ButKeepStatus()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo original.", null);
        var categoryId = Guid.NewGuid();

        article.Update("Roteiro atualizado", "Conteúdo novo.", categoryId);

        article.Title.Should().Be("Roteiro atualizado");
        article.Content.Should().Be("Conteúdo novo.");
        article.CategoryId.Should().Be(categoryId);
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void TransitionTo_DraftToPublished_ShouldPublish()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);

        article.TransitionTo(KnowledgeArticleStatus.Published);

        article.Status.Should().Be(KnowledgeArticleStatus.Published);
    }

    [Fact]
    public void TransitionTo_PublishedToArchived_ShouldArchive()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);
        article.TransitionTo(KnowledgeArticleStatus.Published);

        article.TransitionTo(KnowledgeArticleStatus.Archived);

        article.Status.Should().Be(KnowledgeArticleStatus.Archived);
    }

    [Fact]
    public void TransitionTo_ArchivedToDraft_ShouldRestoreDraft()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        article.TransitionTo(KnowledgeArticleStatus.Archived);

        article.TransitionTo(KnowledgeArticleStatus.Draft);

        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void TransitionTo_DraftToArchived_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Archived);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Draft' → 'Archived'.");
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void TransitionTo_PublishedToDraft_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);
        article.TransitionTo(KnowledgeArticleStatus.Published);

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Draft);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Published' → 'Draft'.");
    }

    [Fact]
    public void TransitionTo_SameState_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);
        article.TransitionTo(KnowledgeArticleStatus.Published);

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Published);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Published' → 'Published'.");
    }
}
