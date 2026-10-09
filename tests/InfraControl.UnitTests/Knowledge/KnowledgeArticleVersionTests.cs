using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F24-T003/K3: numeração de versões e snapshot imutável (spec
/// docs/32 §8 — cadeia v1.0 → v1.1 → v2.0, com aprovador carimbado na
/// aprovação do K2/ADR-034; a "alteração" da §8 é o próprio snapshot).
/// </summary>
public sealed class KnowledgeArticleVersionTests
{
    private static KnowledgeArticle NewArticle(string? tag = null)
        => KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null,
            tags: tag is null ? null : [tag]);

    [Fact]
    public void NewArticle_ShouldStartAtVersion10()
    {
        var article = NewArticle();

        article.Version.Should().Be("1.0");
    }

    [Fact]
    public void BumpVersion_BeforeApproval_ShouldOpenMinor()
    {
        var article = NewArticle();

        article.BumpVersion();
        article.Version.Should().Be("1.1");

        article.BumpVersion();
        article.Version.Should().Be("1.2");
    }

    [Fact]
    public void BumpVersion_AfterApproval_ShouldOpenNewMajor()
    {
        // Cadeia da §8: v1.0 → v1.1 → (aprovada) → v2.0.
        var article = NewArticle();
        article.BumpVersion(); // 1.1
        article.MarkVersionApproved();

        article.BumpVersion();
        article.Version.Should().Be("2.0");
    }

    [Fact]
    public void MarkVersionApproved_Repeated_ShouldNotRegress()
    {
        var article = NewArticle();
        article.MarkVersionApproved();
        article.MarkVersionApproved(); // piso nunca recua

        article.BumpVersion();
        article.Version.Should().Be("2.0");

        article.MarkVersionApproved(); // aprovada em 2.0 → piso 3
        article.BumpVersion();
        article.Version.Should().Be("3.0");
    }

    [Fact]
    public void SnapshotOf_ShouldFreezeStateIncludingTags()
    {
        var author = Guid.NewGuid();
        var article = NewArticle(tag: "redes");

        var snapshot = KnowledgeArticleVersion.SnapshotOf(
            article, "Criação do artigo", author);

        snapshot.Version.Should().Be("1.0");
        snapshot.Title.Should().Be("Roteiro");
        snapshot.Content.Should().Be("Conteúdo.");
        snapshot.Tags.Should().Equal("redes");
        snapshot.AuthorId.Should().Be(author);
        snapshot.ApprovedById.Should().BeNull();

        article.Update("Outro título", "Outro conteúdo.", null);

        snapshot.Title.Should().Be("Roteiro"); // imutável
        snapshot.Content.Should().Be("Conteúdo.");
    }

    [Fact]
    public void SnapshotOf_BlankReason_ShouldThrowPtBr()
    {
        var article = NewArticle();

        var act = () => KnowledgeArticleVersion.SnapshotOf(article, "   ", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O motivo da alteração é obrigatório.*");
    }

    [Fact]
    public void SnapshotOf_LongReason_ShouldThrowPtBr()
    {
        var article = NewArticle();

        var act = () => KnowledgeArticleVersion.SnapshotOf(
            article, new string('x', 501), null);

        act.Should().Throw<ArgumentException>()
            .WithMessage(
                "O motivo da alteração pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Approve_ShouldKeepFirstApprover()
    {
        var first = Guid.NewGuid();
        var snapshot = KnowledgeArticleVersion.SnapshotOf(
            NewArticle(), "Criação do artigo", Guid.NewGuid());

        snapshot.Approve(first);
        snapshot.Approve(Guid.NewGuid());

        snapshot.ApprovedById.Should().Be(first);
        snapshot.ApprovedAt.Should().NotBeNull();
    }
}
