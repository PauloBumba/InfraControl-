using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F24-T003/K3 (ADR-040): snapshot de versão do artigo — cópia integral
/// do conteúdo com os 5 campos do docs/32 §8 (alteração/autor/data/
/// motivo/aprovador) e regras de numeração (edição = minor+1,
/// publicação = major+1).
/// </summary>
public sealed class KnowledgeArticleVersionTests
{
    private static KnowledgeArticle ArticlePublished()
    {
        var article = KnowledgeArticle.Create(
            "Roteiro", "Conteúdo v1.", null, "Guia",
            description: "Descrição.", tags: ["Redes"]);
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        return article;
    }

    private static KnowledgeArticleVersion Latest(int major, int minor)
        => KnowledgeArticleVersion.Create(
            ArticlePublished(), major, minor, Guid.NewGuid(), "Motivo.");

    [Fact]
    public void Create_ShouldCopyArticleContentAndMetadata()
    {
        var article = ArticlePublished();
        var authorId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var version = KnowledgeArticleVersion.Create(
            article, 1, 0, authorId, "Publicação inicial", approverId);

        version.ArticleId.Should().Be(article.Id);
        version.Major.Should().Be(1);
        version.Minor.Should().Be(0);
        version.Version.Should().Be("1.0");
        version.Title.Should().Be("Roteiro");
        version.Content.Should().Be("Conteúdo v1.");
        version.Type.Should().Be("Guia");
        version.Description.Should().Be("Descrição.");
        version.Tags.Should().Equal("redes");
        version.CategoryId.Should().BeNull();
        version.ResponsibleId.Should().BeNull();
        version.Status.Should().Be(KnowledgeArticleStatus.Published);
        version.AuthorId.Should().Be(authorId);
        version.ApprovedById.Should().Be(approverId);
        version.Reason.Should().Be("Publicação inicial");
    }

    [Fact]
    public void Create_MissingReason_ShouldThrowArgumentPtBr()
    {
        var article = ArticlePublished();

        var act = () => KnowledgeArticleVersion.Create(article, 1, 0, null, "  ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O motivo da alteração é obrigatório.*");
    }

    [Fact]
    public void Create_ReasonTooLong_ShouldThrowArgumentPtBr()
    {
        var article = ArticlePublished();

        var act = () => KnowledgeArticleVersion.Create(
            article, 1, 0, null, new string('x', 501));

        act.Should().Throw<ArgumentException>()
            .WithMessage("O motivo da alteração pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void NextForEdit_WithoutVersions_ShouldStartAt10()
    {
        var (major, minor) = KnowledgeArticleVersion.NextForEdit(null);

        major.Should().Be(1);
        minor.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 2, 1, 3)]
    [InlineData(2, 0, 2, 1)]
    public void NextForEdit_ShouldIncrementMinorOnly(
        int major, int minor, int expectedMajor, int expectedMinor)
    {
        var (nextMajor, nextMinor) =
            KnowledgeArticleVersion.NextForEdit(Latest(major, minor));

        nextMajor.Should().Be(expectedMajor);
        nextMinor.Should().Be(expectedMinor);
    }

    [Fact]
    public void NextForPublication_WithoutVersions_ShouldStartAt10()
    {
        var (major, minor) = KnowledgeArticleVersion.NextForPublication(null);

        major.Should().Be(1);
        minor.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 0, 2, 0)]
    [InlineData(1, 3, 2, 0)]
    [InlineData(2, 1, 3, 0)]
    public void NextForPublication_ShouldIncrementMajorResetMinor(
        int major, int minor, int expectedMajor, int expectedMinor)
    {
        var (nextMajor, nextMinor) =
            KnowledgeArticleVersion.NextForPublication(Latest(major, minor));

        nextMajor.Should().Be(expectedMajor);
        nextMinor.Should().Be(expectedMinor);
    }
}
