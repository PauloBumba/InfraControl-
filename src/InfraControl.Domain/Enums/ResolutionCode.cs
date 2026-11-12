namespace InfraControl.Domain.Enums;

/// <summary>
/// Motivo da baixa (F52 — padrão ServiceNow): select obrigatório ao
/// concluir Task/Alert/Incident. Guardado como string no banco e no
/// wire (ADR-061); <see cref="ResolutionCodes.Parse"/> valida a
/// entrada com 400 pt-BR.
/// </summary>
public enum ResolutionCode
{
    /// <summary>Corrigido — causa eliminada.</summary>
    Fixed,

    /// <summary>Contorno — mitigado sem eliminação da causa.</summary>
    Workaround,

    /// <summary>Duplicado — mesmo caso já coberto por outro registro.</summary>
    Duplicate,

    /// <summary>Sem ação necessária — nada a fazer (falso positivo etc.).</summary>
    NoAction,

    /// <summary>Outro — justificativa livre na nota.</summary>
    Other,
}
