namespace InfraControl.Domain.Enums;

/// <summary>
/// Status de um impedimento/dificuldade (docs/30 §11). Aberto →
/// Resolvido (com data de resolução); reabertura limpa a data.
/// </summary>
public enum PdiImpedimentStatus
{
    Open = 1,
    Resolved = 2
}
