using InfraControl.Domain.Entities;

namespace InfraControl.Application.Projects;

/// <summary>
/// Cálculo de avanço do Projeto a partir das fases (ADR-043 · F28-T003).
/// Ponderação uniforme: média aritmética dos percentuais das fases
/// (mesmo critério do dashboard PDI).
/// </summary>
public static class ProjectProgressCalculator
{
    public static int ComputeOverallPercent(IEnumerable<ProjectPhase> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);

        var list = phases as IList<ProjectPhase> ?? phases.ToList();
        if (list.Count == 0)
            return 0;

        return (int)Math.Round(
            list.Average(phase => (double)phase.Percent),
            MidpointRounding.AwayFromZero);
    }
}
