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
            categoryId,
            "Procedimento");

        article.Title.Should().Be("Como trocar o switch central");
        article.Content.Should().Be("Passo a passo da troca com failover.");
        article.CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public void Create_ShouldStartAsDraft()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");

        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void Create_WithoutCategory_ShouldAcceptNull()
    {
        var article = KnowledgeArticle.Create("Sem categoria", "Conteúdo.", null, "Procedimento");

        article.CategoryId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimTitle()
    {
        var article = KnowledgeArticle.Create("  Roteiro  ", "Conteúdo.", null, "Procedimento");

        article.Title.Should().Be("Roteiro");
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("   ", "Conteúdo.", null, "Procedimento");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O título do artigo é obrigatório.*");
    }

    [Fact]
    public void Create_TitleOver200_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create(new string('a', 201), "Conteúdo.", null, "Procedimento");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O título do artigo pode ter no máximo 200 caracteres.*");
    }

    [Fact]
    public void Create_EmptyContent_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("Roteiro", "   ", null, "Procedimento");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O conteúdo do artigo é obrigatório.*");
    }

    [Fact]
    public void Create_ContentOver20000_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("Roteiro", new string('c', 20_001), null, "Procedimento");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O conteúdo do artigo pode ter no máximo 20000 caracteres.*");
    }

    /// <summary>Update não mexe no Status — transições são F10-T003.</summary>
    [Fact]
    public void Update_ShouldChangeTitleContentCategory_ButKeepStatus()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo original.", null, "Procedimento");
        var categoryId = Guid.NewGuid();

        article.Update("Roteiro atualizado", "Conteúdo novo.", categoryId, "Manual",
            reason: "Atualização de teste.");

        article.Title.Should().Be("Roteiro atualizado");
        article.Content.Should().Be("Conteúdo novo.");
        article.CategoryId.Should().Be(categoryId);
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    // ── F24-T002: workflow de 8 estados (ADR-039, docs/32 §7) ─────────────

    [Fact]
    public void TransitionTo_FullEditorialFlow_ShouldPublishAndUpdate()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");

        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Updated);

        article.Status.Should().Be(KnowledgeArticleStatus.Updated);
    }

    [Fact]
    public void TransitionTo_PublishedToResidualStates_ShouldArchiveOrObsolete()
    {
        var archived = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
        archived.TransitionTo(KnowledgeArticleStatus.InReview);
        archived.TransitionTo(KnowledgeArticleStatus.Approved);
        archived.TransitionTo(KnowledgeArticleStatus.Published);
        archived.TransitionTo(KnowledgeArticleStatus.Archived);
        archived.Status.Should().Be(KnowledgeArticleStatus.Archived);

        var obsolete = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
        obsolete.TransitionTo(KnowledgeArticleStatus.InReview);
        obsolete.TransitionTo(KnowledgeArticleStatus.Approved);
        obsolete.TransitionTo(KnowledgeArticleStatus.Published);
        obsolete.TransitionTo(KnowledgeArticleStatus.Obsolete);
        obsolete.Status.Should().Be(KnowledgeArticleStatus.Obsolete);
    }

    [Fact]
    public void TransitionTo_ResidualStatesToDraft_ShouldReopen()
    {
        foreach (var residual in new[]
        {
            KnowledgeArticleStatus.Archived,
            KnowledgeArticleStatus.Obsolete,
            KnowledgeArticleStatus.Rejected,
        })
        {
            var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
            article.TransitionTo(KnowledgeArticleStatus.InReview);
            if (residual == KnowledgeArticleStatus.Rejected)
            {
                article.TransitionTo(KnowledgeArticleStatus.Rejected);
            }
            else
            {
                article.TransitionTo(KnowledgeArticleStatus.Approved);
                article.TransitionTo(KnowledgeArticleStatus.Published);
                article.TransitionTo(residual);
            }

            article.TransitionTo(KnowledgeArticleStatus.Draft);

            article.Status.Should().Be(KnowledgeArticleStatus.Draft);
        }
    }

    [Fact]
    public void TransitionTo_ReviewBranches_ShouldAllowUpdateOrReject()
    {
        var updated = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
        updated.TransitionTo(KnowledgeArticleStatus.InReview);
        updated.TransitionTo(KnowledgeArticleStatus.Updated);
        updated.Status.Should().Be(KnowledgeArticleStatus.Updated);

        var rejected = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
        rejected.TransitionTo(KnowledgeArticleStatus.InReview);
        rejected.TransitionTo(KnowledgeArticleStatus.Rejected);
        rejected.Status.Should().Be(KnowledgeArticleStatus.Rejected);
    }

    [Fact]
    public void TransitionTo_DraftToArchived_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Archived);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Rascunho' → 'Arquivada'.");
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    /// <summary>Ciclo antigo removido: publicar exige passar por revisão.</summary>
    [Fact]
    public void TransitionTo_DraftToPublished_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Published);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Rascunho' → 'Publicada'.");
        article.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public void TransitionTo_PublishedToDraft_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Draft);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Publicada' → 'Rascunho'.");
    }

    [Fact]
    public void TransitionTo_SameState_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
        article.TransitionTo(KnowledgeArticleStatus.InReview);
        article.TransitionTo(KnowledgeArticleStatus.Approved);
        article.TransitionTo(KnowledgeArticleStatus.Published);

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Published);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Publicada' → 'Publicada'.");
    }

    [Fact]
    public void TransitionTo_ReviewToDraft_ShouldThrowConflictPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Procedimento");
        article.TransitionTo(KnowledgeArticleStatus.InReview);

        var act = () => article.TransitionTo(KnowledgeArticleStatus.Draft);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transição inválida: 'Em revisão' → 'Rascunho'.");
    }

    [Theory]
    [InlineData(KnowledgeArticleStatus.Draft, false)]
    [InlineData(KnowledgeArticleStatus.InReview, false)]
    [InlineData(KnowledgeArticleStatus.Approved, false)]
    [InlineData(KnowledgeArticleStatus.Rejected, false)]
    [InlineData(KnowledgeArticleStatus.Published, true)]
    [InlineData(KnowledgeArticleStatus.Updated, true)]
    [InlineData(KnowledgeArticleStatus.Archived, true)]
    [InlineData(KnowledgeArticleStatus.Obsolete, true)]
    public void IsVisibleToReaders_ShouldMatchPublishRule(
        KnowledgeArticleStatus status, bool visible)
        => status.IsVisibleToReaders().Should().Be(visible);

    [Fact]
    public void Update_MissingReason_ShouldThrowArgumentPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Guia");

        var act = () => article.Update(
            "Roteiro", "Conteúdo.", null, "Guia", reason: "   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O motivo da alteração é obrigatório.*");
        article.Title.Should().Be("Roteiro");
    }

    [Fact]
    public void Update_ReasonTooLong_ShouldThrowArgumentPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Guia");

        var act = () => article.Update(
            "Roteiro", "Conteúdo.", null, "Guia", reason: new string('x', 501));

        act.Should().Throw<ArgumentException>()
            .WithMessage("O motivo da alteração pode ter no máximo 500 caracteres.*");
    }

    // ── F24-T001: tipos e identificação (K1 — docs/32 §5–§6) ──────────────

    [Fact]
    public void Create_ShouldPersistTypeDescriptionTagsAuthorResponsible()
    {
        var authorId = Guid.NewGuid();
        var responsibleId = Guid.NewGuid();

        var article = KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null,
            type: "Guia",
            description: "Guia de failover do switch central.",
            tags: ["Redes", "Switch"],
            authorId: authorId,
            responsibleId: responsibleId);

        article.Type.Should().Be("Guia");
        article.Description.Should().Be("Guia de failover do switch central.");
        article.AuthorId.Should().Be(authorId);
        article.ResponsibleId.Should().Be(responsibleId);
        article.Tags.Select(tag => tag.Tag).Should().Equal("redes", "switch");
    }

    [Fact]
    public void Create_UnknownType_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Receita");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tipo inválido: 'Receita'. Use:*");
    }

    [Fact]
    public void Create_EmptyType_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O tipo do artigo é obrigatório.*");
    }

    [Fact]
    public void Create_Type_ShouldNormalizeCaseToCanonical()
    {
        var article = KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null, type: "sOLUçãO CONHECIDA");

        article.Type.Should().Be("Solução conhecida");
    }

    [Fact]
    public void Create_Tags_ShouldTrimLowercaseAndDedupeCaseInsensitive()
    {
        var article = KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null,
            type: "FAQ",
            tags: ["  Backup ", "backup", "RESTAURAÇÃO"]);

        article.Tags.Select(tag => tag.Tag).Should().Equal("backup", "restauração");
    }

    [Fact]
    public void Create_OverMaxTags_ShouldThrowPtBr()
    {
        var tags = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToArray();

        var act = () => KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null, type: "FAQ", tags: tags);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O artigo pode ter no máximo 10 tags.*");
    }

    [Fact]
    public void Create_TagOver30Chars_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null, type: "FAQ",
            tags: [new string('t', 31)]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("A tag pode ter no máximo 30 caracteres.*");
    }

    [Fact]
    public void Create_EmptyTag_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null, type: "FAQ", tags: ["   "]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("A tag não pode ser vazia.*");
    }

    [Fact]
    public void Create_DescriptionOver500_ShouldThrowPtBr()
    {
        var act = () => KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null, type: "FAQ",
            description: new string('d', 501));

        act.Should().Throw<ArgumentException>()
            .WithMessage("A descrição do artigo pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Update_ShouldReplaceIdentificationAndTags()
    {
        var article = KnowledgeArticle.Create(
            "Roteiro", "Conteúdo.", null,
            type: "Guia", description: "Descrição velha", tags: ["antiga"]);
        var responsibleId = Guid.NewGuid();

        article.Update(
            "Roteiro", "Conteúdo.", null,
            type: "Política",
            description: null,
            tags: ["Nova", "nova"],
            responsibleId: responsibleId,
            reason: "Troca de tipo e tags.");

        article.Type.Should().Be("Política");
        article.Description.Should().BeNull();
        article.ResponsibleId.Should().Be(responsibleId);
        article.Tags.Select(tag => tag.Tag).Should().Equal("nova");
    }

    [Fact]
    public void Update_UnknownType_ShouldThrowPtBr()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Guia");

        var act = () => article.Update("Roteiro", "Conteúdo.", null, "OutroTipo",
            reason: "Motivo.");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tipo inválido: 'OutroTipo'. Use:*");
    }

    [Fact]
    public void Update_TypeIsRequired_EvenForLegacyNull()
    {
        var article = KnowledgeArticle.Create("Roteiro", "Conteúdo.", null, "Guia");

        var act = () => article.Update("Roteiro", "Conteúdo.", null, "  ",
            reason: "Motivo.");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O tipo do artigo é obrigatório.*");
    }
}
