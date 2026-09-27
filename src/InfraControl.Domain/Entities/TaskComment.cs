using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Comentário livre em uma Task (F7-T005).</summary>
public sealed class TaskComment : Entity
{
    public const int ContentMaxLength = 500;

    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;

    private TaskComment() { }

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

        return new TaskComment
        {
            TaskId = taskId,
            AuthorId = authorId,
            Content = content
        };
    }
}
