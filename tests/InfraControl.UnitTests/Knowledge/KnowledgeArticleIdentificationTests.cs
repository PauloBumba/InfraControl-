using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F24-T001/K1: identificação do artigo (spec docs/32 §5–§6) — tipo,
/// descrição, tags (normalização e limites) e autor imutável.
/// </summary>
public sealed class KnowledgeArticleIdentificationTests
{
    [Fact]
    public void Create_ShouldCarryIdentification()
    {
        var responsibleId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var article = KnowledgeArticle.Create(
            "Troca do switch central",
            "Conteúdo.",
            categoryId: null,
            KnowledgeArticleType.Procedure,
            "Como trocar o switch com failover.",
            ["redes", "switch"],
            responsibleId,
            authorId);

        article.Type.Should().Be(KnowledgeArticleType.Procedure);
        article.Description.Should().Be("Como trocar o switch com failover.");
        article.Tags.Should().Equal("redes", "switch");
        article.ResponsibleId.Should().Be(responsibleId);
        article.AuthorId.Should().Be(authorId);
    }

    [Fact]
    public void Create_WithoutIdentification_ShouldAcceptNulls()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);

        article.Type.Should().BeNull();
        article.Description.Should().BeNull();
        article.Tags.Should().BeEmpty();
        article.ResponsibleId.Should().BeNull();
        article.AuthorId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldNormalizeTags()
    {
        var article = KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null,
            tags: ["  Redes ", "", "backup", "REDES", "   "]);

        // trim + lowercase + vazios fora + dedup na ordem de chegada.
        article.Tags.Should().Equal("redes", "backup");
    }

    [Fact]
    public void Description_Over500_ShouldThrowPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);

        var set = () => article.SetIdentification(
            null, new string('a', 501), null, null);

        set.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do artigo pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Tag_Over50Chars_ShouldThrowPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);

        var set = () => article.SetIdentification(
            null, null, [new string('t', 51)], null);

        set.Should().Throw<ArgumentException>()
            .WithMessage("Cada tag pode ter no máximo 50 caracteres.*");
    }

    [Fact]
    public void Over20Tags_ShouldThrowPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null);
        var tags = Enumerable.Range(1, 21).Select(i => $"tag{i}").ToArray();

        var set = () => article.SetIdentification(null, null, tags, null);

        set.Should().Throw<ArgumentException>()
            .WithMessage("O artigo pode ter no máximo 20 tags.*");
    }

    [Fact]
    public void SetIdentification_ShouldNotChangeAuthor()
    {
        var authorId = Guid.NewGuid();
        var article = KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null, authorId: authorId);

        article.SetIdentification(
            KnowledgeArticleType.Guide, "Descrição nova", ["guia"], Guid.NewGuid());

        article.AuthorId.Should().Be(authorId);
        article.Type.Should().Be(KnowledgeArticleType.Guide);
        article.Description.Should().Be("Descrição nova");
        article.Tags.Should().Equal("guia");
    }
}
