using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Tasks.Queries;

// F23-T004/P4d — timeline unificada tipada (Plano de Execução §8):
// composição em LEITURA de histórico + comentários (impedimentos,
// evidências, SLA, fases etc. entram pelo histórico — mesmas fontes,
// sem duplicata; comentário sai da fonte rica e a ação `Commented` do
// histórico é descartada). Sem tabela nova. Filtro opcional `types`
// (comma-separated; tipo desconhecido → 400 pt-BR) e `limit`
// (clamp 1..500, default 100). Mais recente primeiro.

public sealed record GetTaskTimelineQuery(
    Guid TaskId,
    string? Types = null,
    int Limit = 100) : IQuery<IReadOnlyCollection<TaskTimelineEntryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetTaskTimelineQueryHandler(
    ITaskItemRepository taskRepository,
    ITaskHistoryRepository historyRepository,
    ITaskCommentRepository commentRepository)
    : IRequestHandler<GetTaskTimelineQuery, IReadOnlyCollection<TaskTimelineEntryDto>>
{
    public const int MaxLimit = 500;

    public async Task<IReadOnlyCollection<TaskTimelineEntryDto>> Handle(
        GetTaskTimelineQuery request, CancellationToken cancellationToken)
    {
        _ = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var types = ParseTypes(request.Types);

        var entries = new List<TaskTimelineEntryDto>();

        var history = await historyRepository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);
        foreach (var row in history)
        {
            var action = row.Entry.Action;

            // O comentário aparece pela fonte rica (abaixo) — a ação
            // `Commented` do histórico duplicaria a mesma entrada.
            if (action == TaskHistoryAction.Commented)
                continue;

            var type = TaskTimelineTypes.ForAction(action);
            if (types is not null && !types.Contains(type))
                continue;

            entries.Add(new TaskTimelineEntryDto(
                $"h:{row.Entry.Id}",
                type,
                action.ToString(),
                row.Entry.Detail,
                null,
                row.Entry.CreateDateTime,
                row.Entry.ActorId,
                row.ActorName));
        }

        var comments = await commentRepository.GetByTaskIdAsync(
            request.TaskId, cancellationToken);
        foreach (var row in comments)
        {
            if (types is not null && !types.Contains(TaskTimelineTypes.Comment))
                continue;

            entries.Add(new TaskTimelineEntryDto(
                $"c:{row.Comment.Id}",
                TaskTimelineTypes.Comment,
                null,
                null,
                row.Comment.Content,
                row.Comment.CreateDateTime,
                row.Comment.AuthorId,
                row.AuthorName));
        }

        return entries
            .OrderByDescending(entry => entry.At)
            .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    /// <summary>null = todos os tipos; vazio/nulo vira null; inválido = 400.</summary>
    private static HashSet<string>? ParseTypes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var parts = raw.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var invalid = parts
            .Where(part => !TaskTimelineTypes.All.Contains(part))
            .ToArray();
        if (invalid.Length > 0)
            throw new ArgumentException(
                $"Tipo de timeline inválido: {string.Join(", ", invalid)}."
                + $" Aceitos: {string.Join(", ", TaskTimelineTypes.All)}.");

        return new HashSet<string>(parts, StringComparer.Ordinal);
    }
}
