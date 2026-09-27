using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>F7-T005: comentários da task em ordem cronológica, com autor.</summary>
public sealed class TaskCommentRepository(AppDbContext dbContext)
    : ITaskCommentRepository
{
    public async Task<TaskComment?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
        => await dbContext.TaskComments
            .FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);

    public async Task AddAsync(
        TaskComment entity, CancellationToken cancellationToken = default)
        => await dbContext.TaskComments.AddAsync(entity, cancellationToken);

    public void Remove(TaskComment entity)
        => dbContext.TaskComments.Remove(entity);

    public async Task<IReadOnlyCollection<TaskCommentWithAuthor>> GetByTaskIdAsync(
        Guid taskId, CancellationToken cancellationToken = default)
    {
        // Left join: autor pode ter saído — nome vira null, comentário fica.
        var rows = await (
            from comment in dbContext.TaskComments.AsNoTracking()
            where comment.TaskId == taskId
            orderby comment.CreateDateTime
            join author in dbContext.DomainUsers.AsNoTracking()
                on comment.AuthorId equals author.Id into authorGroup
            from author in authorGroup.DefaultIfEmpty()
            select new
            {
                Comment = comment,
                AuthorName = author != null ? author.DisplayName : null
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new TaskCommentWithAuthor(row.Comment, row.AuthorName))
            .ToArray();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
