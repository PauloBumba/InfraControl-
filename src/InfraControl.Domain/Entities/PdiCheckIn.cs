using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Check-in bilateral da fase (docs/30 §10, §23 PDICheckIn; Fase 3 —
/// ADR-033): o colaborador preenche o relato (o que realizou, o que
/// aprendeu, dificuldade, suporte) e o gestor responde (feedback +
/// próxima ação) na mesma entidade — a resposta carrega
/// RespondedAt/RespondedById. Não é append-only: o relato pode ser
/// editado pelo dono enquanto a conversa evolui.
/// </summary>
public sealed class PdiCheckIn : Entity
{
    public const int TextMaxLength = 2000;

    public Guid PhaseId { get; private set; }
    public DateOnly CheckInDate { get; private set; }
    public Guid CreatedBy { get; private set; }

    public string WhatDone { get; private set; } = string.Empty;
    public string? WhatLearned { get; private set; }
    public string? Difficulty { get; private set; }
    public string? NeedSupport { get; private set; }

    public string? ManagerFeedback { get; private set; }
    public string? NextAction { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public Guid? RespondedById { get; private set; }

    private PdiCheckIn() { }

    public static PdiCheckIn Create(
        Guid phaseId,
        DateOnly checkInDate,
        Guid createdBy,
        string whatDone,
        string? whatLearned = null,
        string? difficulty = null,
        string? needSupport = null)
    {
        if (phaseId == Guid.Empty)
            throw new ArgumentException(
                "A fase do check-in é obrigatória.", nameof(phaseId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException(
                "O autor do check-in é obrigatório.", nameof(createdBy));

        var checkIn = new PdiCheckIn
        {
            PhaseId = phaseId,
            CheckInDate = checkInDate,
            CreatedBy = createdBy
        };
        checkIn.EditRelato(whatDone, whatLearned, difficulty, needSupport);
        return checkIn;
    }

    /// <summary>Lado do colaborador (§10 — "o que realizou? ...").</summary>
    public void EditRelato(
        string whatDone, string? whatLearned, string? difficulty, string? needSupport)
    {
        if (string.IsNullOrWhiteSpace(whatDone))
            throw new ArgumentException(
                "O relato do que foi realizado é obrigatório.", nameof(whatDone));

        WhatDone = PdiCompetency.Normalize(
            whatDone, TextMaxLength, nameof(whatDone))!;
        WhatLearned = PdiCompetency.Normalize(whatLearned, TextMaxLength, nameof(whatLearned));
        Difficulty = PdiCompetency.Normalize(difficulty, TextMaxLength, nameof(difficulty));
        NeedSupport = PdiCompetency.Normalize(needSupport, TextMaxLength, nameof(needSupport));
        Touch();
    }

    /// <summary>
    /// Lado do gestor (§10 — feedback + próxima ação). Zerar os dois
    /// limpa a resposta (desfazer); gravar qualquer um marca o instante.
    /// </summary>
    public void Respond(
        string? managerFeedback, string? nextAction, Guid respondedById)
    {
        if (respondedById == Guid.Empty)
            throw new ArgumentException(
                "O respondente do check-in é obrigatório.", nameof(respondedById));

        ManagerFeedback = PdiCompetency.Normalize(
            managerFeedback, TextMaxLength, nameof(managerFeedback));
        NextAction = PdiCompetency.Normalize(nextAction, TextMaxLength, nameof(nextAction));

        var answered = ManagerFeedback is not null || NextAction is not null;
        RespondedAt = answered ? DateTime.UtcNow : null;
        RespondedById = answered ? respondedById : null;
        Touch();
    }
}
