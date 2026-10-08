using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F23-T005/P5a: vínculo Task ↔ KnowledgeArticle — guards pt-BR.</summary>
public sealed class TaskKnowledgeArticleTests
{
    [Fact]
    public void Create_ShouldPairTaskAndArticle()
    {
        var taskId = Guid.NewGuid();
        var articleId = Guid.NewGuid();

        var link = TaskKnowledgeArticle.Create(taskId, articleId);

        link.TaskId.Should().Be(taskId);
        link.ArticleId.Should().Be(articleId);
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskKnowledgeArticle.Create(Guid.Empty, Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyArticleId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskKnowledgeArticle.Create(Guid.NewGuid(), Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do artigo é obrigatório.*");
    }
}
