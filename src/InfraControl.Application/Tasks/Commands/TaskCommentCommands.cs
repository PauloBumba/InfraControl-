using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Tasks.Commands;

// F7-T005: comentário livre em uma Task — autor = usuário logado
// (ICurrentUserContext); task inexistente → 404 pt-BR; conteúdo vazio/
// acima do limite → 400 pt-BR (domínio).

public sealed record AddTaskCommentCommand(
    Guid TaskId,
    string Content) : BaseCommand<TaskCommentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.TasksManage.ToString()];
}

public sealed class AddTaskCommentCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskCommentRepository commentRepository,
    ICurrentUserContext currentUser,
    ILogger<AddTaskCommentCommandHandler> logger)
    : IRequestHandler<AddTaskCommentCommand, TaskCommentDto>
{
    public async Task<TaskCommentDto> Handle(
        AddTaskCommentCommand request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var authorId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Autenticação é necessária para esta requisição.");

        var comment = TaskComment.Create(request.TaskId, authorId, request.Content);

        await commentRepository.AddAsync(comment, cancellationToken);
        await commentRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Comment {CommentId} added to task {TaskId}.",
            comment.Id, request.TaskId);

        // Nome do autor é o do contexto (usuário logado) — join do
        // repositório acontece na leitura.
        return new TaskCommentDto(
            comment.Id, comment.TaskId, comment.AuthorId,
            currentUser.UserName, comment.Content, comment.CreateDateTime);
    }
}
