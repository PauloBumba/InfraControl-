using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Solicitação formal de conclusão de um Projeto (spec evolução 2026-11-07):
/// o responsável descreve o trabalho realizado e o administrador decide —
/// aprovar gera o registro na base de conhecimento; reprovar ou solicitar
/// ajustes devolve o projeto para InProgress. Uma conclusão aprovada é
/// imutável; novas rodadas de revisão geram novos registros.
/// </summary>
public sealed class ProjectConclusion : Entity
{
    public const int DescriptionMaxLength = 4000;
    public const int ActivitiesMaxLength = 4000;
    public const int ResultsMaxLength = 4000;
    public const int ProblemsMaxLength = 4000;
    public const int SolutionsMaxLength = 4000;
    public const int EvidenceSummaryMaxLength = 2000;
    public const int DecisionNotesMaxLength = 2000;

    public Guid ProjectId { get; private set; }
    public ProjectConclusionStatus Status { get; private set; }

    /// <summary>Descrição do que foi realizado.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Atividades executadas.</summary>
    public string ActivitiesPerformed { get; private set; } = string.Empty;

    /// <summary>Resultados obtidos.</summary>
    public string ResultsAchieved { get; private set; } = string.Empty;

    /// <summary>Problemas encontrados (opcional).</summary>
    public string? ProblemsEncountered { get; private set; }

    /// <summary>Soluções implementadas (opcional).</summary>
    public string? SolutionsImplemented { get; private set; }

    /// <summary>Resumo das evidências e documentos anexados (opcional).</summary>
    public string? EvidenceSummary { get; private set; }

    /// <summary>Data e hora UTC declarada como conclusão do trabalho.</summary>
    public DateTimeOffset CompletedAt { get; private set; }

    /// <summary>Usuário que submeteu a solicitação.</summary>
    public Guid SubmittedById { get; private set; }

    /// <summary>Administrador que decidiu (aprovação ou devolução).</summary>
    public Guid? DecidedById { get; private set; }

    /// <summary>Parecer/justificativa da decisão administrativa.</summary>
    public string? DecisionNotes { get; private set; }

    /// <summary>Data e hora UTC da decisão administrativa.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    private ProjectConclusion() { }

    public static ProjectConclusion Submit(
        Guid projectId,
        string description,
        string activitiesPerformed,
        string resultsAchieved,
        string? problemsEncountered,
        string? solutionsImplemented,
        string? evidenceSummary,
        DateTimeOffset completedAt,
        Guid submittedById)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("O projeto da conclusão é obrigatório.", nameof(projectId));
        if (submittedById == Guid.Empty)
            throw new ArgumentException("O usuário que submeteu é obrigatório.", nameof(submittedById));
        if (completedAt == default)
            throw new ArgumentException("A data de conclusão é obrigatória.", nameof(completedAt));

        return new ProjectConclusion
        {
            ProjectId = projectId,
            Status = ProjectConclusionStatus.Pending,
            Description = Require(description, DescriptionMaxLength, "A descrição do que foi realizado é obrigatória.", nameof(description)),
            ActivitiesPerformed = Require(activitiesPerformed, ActivitiesMaxLength, "As atividades executadas são obrigatórias.", nameof(activitiesPerformed)),
            ResultsAchieved = Require(resultsAchieved, ResultsMaxLength, "Os resultados obtidos são obrigatórios.", nameof(resultsAchieved)),
            ProblemsEncountered = Optional(problemsEncountered, ProblemsMaxLength, nameof(problemsEncountered)),
            SolutionsImplemented = Optional(solutionsImplemented, SolutionsMaxLength, nameof(solutionsImplemented)),
            EvidenceSummary = Optional(evidenceSummary, EvidenceSummaryMaxLength, nameof(evidenceSummary)),
            CompletedAt = completedAt,
            SubmittedById = submittedById,
        };
    }

    /// <summary>Aprovação administrativa — gera o registro na base de conhecimento.</summary>
    public void Approve(Guid decidedById, string? notes, DateTimeOffset decidedAt)
    {
        EnsurePending();
        if (decidedById == Guid.Empty)
            throw new ArgumentException("O administrador decisor é obrigatório.", nameof(decidedById));

        Status = ProjectConclusionStatus.Approved;
        DecidedById = decidedById;
        DecisionNotes = Optional(notes, DecisionNotesMaxLength, nameof(notes));
        DecidedAt = decidedAt;
        Touch();
    }

    /// <summary>Reprovação com justificativa — o projeto volta para InProgress.</summary>
    public void Reject(Guid decidedById, string justification, DateTimeOffset decidedAt)
    {
        EnsurePending();
        if (decidedById == Guid.Empty)
            throw new ArgumentException("O administrador decisor é obrigatório.", nameof(decidedById));
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("A justificativa da reprovação é obrigatória.", nameof(justification));

        Status = ProjectConclusionStatus.Rejected;
        DecidedById = decidedById;
        DecisionNotes = Truncate(justification.Trim(), DecisionNotesMaxLength);
        DecidedAt = decidedAt;
        Touch();
    }

    /// <summary>Solicitação de ajustes — o projeto volta para InProgress.</summary>
    public void RequestChanges(Guid decidedById, string justification, DateTimeOffset decidedAt)
    {
        EnsurePending();
        if (decidedById == Guid.Empty)
            throw new ArgumentException("O administrador decisor é obrigatório.", nameof(decidedById));
        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("A justificativa dos ajustes solicitados é obrigatória.", nameof(justification));

        Status = ProjectConclusionStatus.ChangesRequested;
        DecidedById = decidedById;
        DecisionNotes = Truncate(justification.Trim(), DecisionNotesMaxLength);
        DecidedAt = decidedAt;
        Touch();
    }

    private void EnsurePending()
    {
        if (Status != ProjectConclusionStatus.Pending)
            throw new InvalidOperationException(
                $"A conclusão já foi decidida (estado '{Status}').");
    }

    private static string Require(string? value, int max, string message, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, paramName);

        value = value.Trim();
        if (value.Length > max)
            throw new ArgumentException($"O campo excede o limite de {max} caracteres.", paramName);

        return value;
    }

    private static string? Optional(string? value, int max, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.Length > max)
            throw new ArgumentException($"O campo excede o limite de {max} caracteres.", paramName);

        return value;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
