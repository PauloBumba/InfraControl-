using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Tasks;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// F24-T006/K6 (spec docs/32 §2–§4, ADR-035): monta o primeiro rascunho
/// de um artigo Knowledge a partir DOS REGISTROS EXISTENTES de uma
/// Task/PDI concluída — determinístico, sem LLM, sem inventar: seção só
/// existe se houver registro (texto ausente nunca é criado, a seção vazia
/// é omitida). O artigo nasce Draft e segue a revisão normal (ADR-034).
/// </summary>
public static class KnowledgeDraftComposer
{
    /// <summary>Espelha <c>KnowledgeArticle.TitleMaxLength</c>.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Espelha <c>KnowledgeArticle.ContentMaxLength</c>.</summary>
    public const int MaxContentLength = 20000;

    /// <summary>Cap das linhas de tempo (§3 — só os registros mais recentes).</summary>
    private const int MaxTimelineEntries = 50;

    private const string TaskBanner =
        "> Rascunho gerado automaticamente a partir dos registros da Task"
        + " (spec docs/32 §3). Revise antes de publicar.";

    private const string PdiBanner =
        "> Rascunho gerado automaticamente a partir dos registros do PDI"
        + " (spec docs/32 §4). Revise antes de publicar.";

    /// <summary>Rascunho a partir de uma Task concluída (§3).</summary>
    public static (string Title, string Content) ComposeFromTask(
        TaskItem task,
        TaskSubtaskStats subtaskStats,
        IReadOnlyCollection<TaskHistoryWithActor> history,
        IReadOnlyCollection<TaskCriterion> criteria,
        IReadOnlyCollection<TaskPhase> phases,
        IReadOnlyCollection<TaskEvidence> evidences,
        IReadOnlyCollection<TaskCommentWithAuthor> comments,
        IReadOnlyCollection<TaskImpediment> impediments)
    {
        var parts = new List<string> { TaskBanner };

        // Objetivo/resultado: só o que a task registra.
        var objective = new List<string?>();
        if (!string.IsNullOrWhiteSpace(task.Description))
            objective.Add(task.Description.Trim());
        if (task.DueDate is { } due)
            objective.Add($"- Prazo: {due:dd/MM/yyyy}.");
        AddSection(parts, "Objetivo", objective);

        AddSection(parts, "Resultado", new[] { BlankToNull(task.Result) });

        AddSection(parts, "Critérios de aceite", criteria.Select(criterion =>
            $"- [{(criterion.IsDone ? "x" : " ")}] {criterion.Description.Trim()}"));

        AddSection(parts, "Fases", phases
            .OrderBy(phase => phase.Position)
            .Select(phase =>
                $"- {phase.Position}. {phase.Name.Trim()} — "
                + (phase.IsCompleted ? "concluída"
                    : phase.IsCurrent ? "em execução" : "pendente")
                + "."));

        // Sem tabela de subtarefas de verdade: só as estatísticas do
        // repositório (registro real, nada inventado).
        if (subtaskStats.Total > 0)
            AddSection(parts, "Subtarefas", new[]
            {
                $"- {subtaskStats.Done} de {subtaskStats.NonCancelled} concluídas"
                + $" ({subtaskStats.Total} no total).",
            });

        AddSection(parts, "Dificuldades e impedimentos", impediments.Select(impediment =>
            $"- {impediment.Description.Trim()} — "
            + (impediment.IsResolved ? "resolvido" : "aberto")
            + "."));

        AddSection(parts, "Evidências", evidences.Select(evidence =>
            string.IsNullOrWhiteSpace(evidence.Note)
                ? $"- {evidence.FileName}"
                : $"- {evidence.FileName} — {evidence.Note.Trim()}"));

        AddSection(parts, "Comentários", comments
            .OrderBy(row => row.Comment.CreateDateTime)
            .Select(row =>
                $"- {row.Comment.CreateDateTime:dd/MM/yyyy HH:mm} — "
                + $"{row.AuthorName ?? "usuário removido"}: {row.Comment.Content.Trim()}"));

        AddSection(parts, "Linha do tempo", history
            .OrderBy(row => row.Entry.CreateDateTime)
            .TakeLast(MaxTimelineEntries)
            .Select(row =>
                $"- {row.Entry.CreateDateTime:dd/MM/yyyy HH:mm} — "
                + $"{row.ActorName ?? "sistema"} [{TaskTimelineTypes.ForAction(row.Entry.Action)}]"
                + (string.IsNullOrWhiteSpace(row.Entry.Detail)
                    ? "."
                    : $": {row.Entry.Detail.Trim()}")));

        return (
            SafeCut(task.Title.Trim(), MaxTitleLength),
            CapContent(string.Join("\n\n", parts)));
    }

    /// <summary>Rascunho a partir de um PDI concluído (§4).</summary>
    public static (string Title, string Content) ComposeFromPdi(
        IndividualDevelopmentPlan plan,
        IReadOnlyCollection<PdiObjective> objectives,
        IReadOnlyCollection<PdiCompetency> competencies,
        IReadOnlyCollection<PdiHistoryWithActor> history)
    {
        var parts = new List<string> { PdiBanner };

        AddSection(parts, "Plano", new[]
        {
            BlankToNull(plan.Strategy),
            $"- Prazo do plano: {plan.TargetDate:dd/MM/yyyy}.",
        });

        // Objetivos em ordem (§5) — só os campos preenchidos por objetivo.
        var objectiveBlocks = new List<string>();
        foreach (var objective in objectives.OrderBy(item => item.Order))
        {
            var block = new List<string>
            {
                $"### {objective.Order}. {objective.Title.Trim()}",
            };
            if (!string.IsNullOrWhiteSpace(objective.Description))
                block.Add(objective.Description.Trim());
            AddBullet(block, "Situação atual", objective.CurrentSituation);
            AddBullet(block, "Situação desejada", objective.DesiredSituation);
            AddBullet(block, "Resultado esperado", objective.ExpectedResult);
            AddBullet(block, "Indicador", objective.Indicator);
            if (objective.TargetDate is { } target)
                block.Add($"- Prazo: {target:dd/MM/yyyy}.");
            AddBullet(
                block, "Competência",
                objective.CompetencyId is { } competencyId
                    ? competencies.FirstOrDefault(item => item.Id == competencyId)?.Name
                    : null);
            objectiveBlocks.Add(string.Join("\n", block));
        }

        if (objectiveBlocks.Count > 0)
            parts.Add("## Objetivos\n" + string.Join("\n\n", objectiveBlocks));

        AddSection(parts, "Linha do tempo", history
            .OrderBy(row => row.Entry.CreateDateTime)
            .TakeLast(MaxTimelineEntries)
            .Select(row =>
                $"- {row.Entry.CreateDateTime:dd/MM/yyyy HH:mm} — "
                + $"{row.ActorName ?? "sistema"}: {DescribePdiEvent(row.Entry)}"));

        // Título: primeiro objetivo (§4), senão a estratégia do plano.
        var title = objectives.OrderBy(item => item.Order).FirstOrDefault()?.Title
            ?? plan.Strategy;
        return (
            SafeCut(title.Trim(), MaxTitleLength),
            CapContent(string.Join("\n\n", parts)));
    }

    /// <summary>
    /// Adiciona a seção só com as linhas não vazias — seção sem
    /// registro não aparece (§3: "não inventar").
    /// </summary>
    private static void AddSection(
        List<string> parts, string heading, IEnumerable<string?> lines)
    {
        var kept = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line!.Trim())
            .ToArray();
        if (kept.Length == 0)
            return;
        parts.Add($"## {heading}\n" + string.Join("\n", kept));
    }

    private static void AddBullet(List<string> block, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            block.Add($"- {label}: {value.Trim().TrimEnd('.')}.");
    }

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string DescribePdiEvent(PdiHistoryEntry entry)
        => entry.Type switch
        {
            PdiHistoryType.Created => "plano criado.",
            PdiHistoryType.StatusChanged =>
                $"status {entry.FromStatus} → {entry.ToStatus}"
                + (string.IsNullOrWhiteSpace(entry.Reason)
                    ? "."
                    : $" ({entry.Reason.Trim()})."),
            _ => string.IsNullOrWhiteSpace(entry.Detail)
                ? "atualização."
                : entry.Detail.Trim(),
        };

    /// <summary>Corta no limite sem deixar surrogate solto.</summary>
    private static string SafeCut(string value, int max)
    {
        if (value.Length <= max)
            return value;
        var cut = max;
        if (char.IsHighSurrogate(value[cut - 1]))
            cut--;
        return value[..cut];
    }

    /// <summary>Respeita <see cref="MaxContentLength"/> (20k) com aviso de corte.</summary>
    private static string CapContent(string content)
    {
        if (content.Length <= MaxContentLength)
            return content;
        const string suffix = "\n\n> (conteúdo truncado na geração.)";
        var limit = MaxContentLength - suffix.Length - 1;
        var cut = content.LastIndexOf('\n', limit);
        if (cut <= 0)
            cut = limit;
        return content[..cut] + suffix;
    }
}
