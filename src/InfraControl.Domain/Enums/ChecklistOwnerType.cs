namespace InfraControl.Domain.Enums;

/// <summary>
/// Dono de um item de checklist genérico (F50-T007/ADR-067). Guardado
/// como string; a existência do dono é validada no handler por tipo
/// (precedente `KnowledgeOriginType` — extensão futura = valor novo
/// aqui + casa no switch de validação do handler).
///
/// `Task` fica de fora de propósito: o checklist de task continua sendo
/// o `TaskCriterion` (F23-T003) com FK, timeline e UI próprias.
/// </summary>
public enum ChecklistOwnerType
{
    /// <summary>Dispositivo do inventário (Device).</summary>
    Device,

    /// <summary>Localização do inventário (Location).</summary>
    Location,

    /// <summary>Incidente/chamado (Incident).</summary>
    Incident,

    /// <summary>Projeto (Project).</summary>
    Project,
}
