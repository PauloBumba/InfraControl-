using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T002: KnowledgeArticle — invariantes pt-BR (docs/04: artigos
/// Draft/Published/Archived; transições ficam na F10-T003).
/// </summary>
public sealed class KnowledgeArticleTests
{
    [Fact]
    public void Create_ShouldPersistTitleBodyCategoryAndDraftStatus()
    {
        var categoryId = Guid.NewGuid();

        var article = KnowledgeArticle.Create(
            "Como trocar o switch principal",
            "Passo a passo da troca com janela de manutenção.",
            categoryId);

        article.Title.Should().Be("Como trocar o switch principal");
        article.Body.Should().StartWith("Passo a passo");
        article.CategoryId.Should().Be(categoryId);
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void Create_WithoutCategory_ShouldAcceptNull()
    {
        var article = KnowledgeArticle.Create("Título", "Conteúdo.", null);

        article.CategoryId.Should().BeNull();
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
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
        var act = () => KnowledgeArticle.Create(
            new string('t', 201), "Conteúdo.", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O título do artigo pode ter no máximo 200 caracteres.*");
    }

    [Fact]
    public void Create_EmptyBody_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("Título", "   ", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O conteúdo do artigo é obrigatório.*");
    }

    [Fact]
    public void Update_ShouldChangeTitleBodyAndCategory()
    {
        var article = KnowledgeArticle.Create("Título", "Conteúdo.", null);
        var categoryId = Guid.NewGuid();

        article.Update("Título v2", "Conteúdo v2.", categoryId);

        article.Title.Should().Be("Título v2");
        article.Body.Should().Be("Conteúdo v2.");
        article.CategoryId.Should().Be(categoryId);
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void Create_ShouldTrimTitle()
    {
        var article = KnowledgeArticle.Create("  Título  ", "Conteúdo.", null);

        article.Title.Should().Be("Título");
    }
}
