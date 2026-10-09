namespace InfraControl.Api.ViewModels.Pdi;

/// <summary>
/// Avaliação do PDI (PUT /api/v1/pdi/{userId}/assessment, §15.3 —
/// Fase 6): conteúdo qualitativo do lado que o ator representa — dono
/// grava a autoavaliação, gestor/Admin a avaliação do gestor (lado
/// determinado no servidor).
/// </summary>
public sealed record SavePdiAssessmentViewModel(string Content);

/// <summary>
/// Resultado final do PDI (PUT /api/v1/pdi/{userId}/result, §16 —
/// Fase 6): um dos 4 estados do §16 (Atingido, ParcialmenteAtingido,
/// NaoAtingido, Replanejado — case-insensitive) + nota opcional.
/// </summary>
public sealed record FinalizePdiResultViewModel(
    string Outcome,
    string? Notes = null);
