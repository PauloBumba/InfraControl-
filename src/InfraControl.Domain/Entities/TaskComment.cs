using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>Comentário livre em uma Task (F7-T005).</summary>
public sealed class TaskComment : AggregateRoot
{
    public const int ContentMaxLength = 500;

    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;

    private TaskComment() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new TaskCommentDeletedDomainEvent(Id, TaskId));

    public static TaskComment Create(Guid taskId, Guid authorId, string content)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("O id da task é obrigatório.", nameof(taskId));
        if (authorId == Guid.Empty)
            throw new ArgumentException("O id do autor é obrigatório.", nameof(authorId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "O conteúdo do comentário é obrigatório.", nameof(content));

        content = content.Trim();
        if (content.Length > ContentMaxLength)
            throw new ArgumentException(
                $"O comentário pode ter no máximo {ContentMaxLength} caracteres.",
                nameof(content));

        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorId = authorId,
            Content = content
        };
        comment.RaiseDomainEvent(new TaskCommentCreatedDomainEvent(
            comment.Id, comment.TaskId, comment.AuthorId));
        return comment;
    }

    /// <summary>ADR-030 §1 — PUT do comentário (mesmos guards do Create).</summary>
    public void EditContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException(
                "O conteúdo do comentário é obrigatório.", nameof(content));

        content = content.Trim();
        if (content.Length > ContentMaxLength)
            throw new ArgumentException(
                $"O comentário pode ter no máximo {ContentMaxLength} caracteres.",
                nameof(content));

        Content = content;
        RaiseDomainEvent(new TaskCommentUpdatedDomainEvent(Id, TaskId));
    }
}
