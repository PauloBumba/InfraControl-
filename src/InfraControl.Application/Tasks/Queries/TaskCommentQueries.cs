using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Tasks.Queries;

// F7-T005: listagem de comentários da Task (ordem cronológica, com autor).

public sealed record GetTaskCommentsQuery(Guid TaskId)
    : IQuery<IReadOnlyCollection<TaskCommentDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskCommentsQueryHandler(
    ITaskItemRepository taskRepository,
    ITaskCommentRepository repository)
    : IRequestHandler<GetTaskCommentsQuery, IReadOnlyCollection<TaskCommentDto>>
{
    public async Task<IReadOnlyCollection<TaskCommentDto>> Handle(
        GetTaskCommentsQuery request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var rows = await repository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);

        return rows.Select(row => new TaskCommentDto(
                row.Comment.Id,
                row.Comment.TaskId,
                row.Comment.AuthorId,
                row.AuthorName,
                row.Comment.Content,
                row.Comment.CreateDateTime))
            .ToArray();
    }
}
