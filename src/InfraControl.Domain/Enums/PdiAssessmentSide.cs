namespace InfraControl.Domain.Enums;

/// <summary>
/// Lado da avaliação do PDI (docs/30 §15.3, ADR-036): a autoavaliação é
/// do colaborador (dono do plano) e a avaliação do gestor/Admin.
/// Armazenado como string (<c>HasConversion&lt;string&gt;()</c>).
/// </summary>
public enum PdiAssessmentSide
{
    /// <summary>Autoavaliação — lado do colaborador (§15.3).</summary>
    Self = 1,

    /// <summary>Avaliação do gestor — lado do gestor/Admin (§15.3).</summary>
    Manager = 2
}
