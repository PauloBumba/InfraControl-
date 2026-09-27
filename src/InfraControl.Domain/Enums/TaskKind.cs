namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipo da Task (docs/04 — catálogo fechado). `TaskKind` evita a
/// ambiguidade com membros BCL; espelha os "Tipos" do documento.
/// </summary>
public enum TaskKind
{
    Manual,
    Incident,
    Alert,
    Monitoring,
    Discovery,
    Maintenance,
    Change,
    Preventive,
    Corrective,
    Project,
    System
}
