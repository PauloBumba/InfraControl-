namespace InfraControl.Domain.Enums;

/// <summary>Categoria do impedimento/risco do Projeto (ADR-043 · docs/38 §3.4).</summary>
public enum ProjectImpedimentCategory
{
    Technical = 1,
    Vendor = 2,
    Budget = 3,
    SecurityAccess = 4
}
