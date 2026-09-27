using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Tasks;

/// <summary>F7-T005: comentário da Task — guards pt-BR do domínio.</summary>
public sealed class TaskCommentTests
{
    [Fact]
    public void Create_ShouldPairTaskAuthorAndContent()
    {
        var taskId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var comment = TaskComment.Create(taskId, authorId, "  trocar o módulo hoje  ");

        comment.TaskId.Should().Be(taskId);
        comment.AuthorId.Should().Be(authorId);
        comment.Content.Should().Be("trocar o módulo hoje");
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskComment.Create(Guid.Empty, Guid.NewGuid(), "ok");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyAuthorId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskComment.Create(Guid.NewGuid(), Guid.Empty, "ok");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do autor é obrigatório.*");
    }

    [Fact]
    public void Create_BlankContent_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskComment.Create(Guid.NewGuid(), Guid.NewGuid(), "   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("O conteúdo do comentário é obrigatório.*");
    }

    [Fact]
    public void Create_ContentAbove500_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskComment.Create(
            Guid.NewGuid(), Guid.NewGuid(), new string('c', 501));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*máximo 500 caracteres*");
    }
}
