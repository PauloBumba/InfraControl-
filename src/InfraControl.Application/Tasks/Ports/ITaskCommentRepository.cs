using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Tasks.Ports;

/// <summary>Comentário da Task com o nome do autor (F7-T005).</summary>
public sealed record TaskCommentWithAuthor(TaskComment Comment, string? AuthorName);

public interface ITaskCommentRepository : IRepository<TaskComment>
{
    /// <summary>Comentários da task em ordem cronológica (com autor).</summary>
    Task<IReadOnlyCollection<TaskCommentWithAuthor>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
