namespace InfraControl.Application.Sla.Slos.Bindings;

/// <summary>
/// ADR-069: consumo linear do error budget — fração de indisponibilidade
/// observada ÷ fração permitida pelo alvo, em %. Limitado a 100
/// (acima disso o budget está esgotado; a invariante 0–100 do
/// <c>SloBudget</c> é preservada). Alvo 100% = budget zero: qualquer
/// falha consome tudo.
/// </summary>
public static class SloBudgetCalculator
{
    public static decimal ComputeConsumedPercent(
        decimal availabilityPercent, decimal targetPercent)
    {
        var unavailability = 100m - availabilityPercent;
        if (unavailability <= 0)
            return 0m;

        var allowedUnavailability = 100m - targetPercent;
        if (allowedUnavailability <= 0)
            return 100m;

        var consumed = unavailability / allowedUnavailability * 100m;
        return consumed >= 100m ? 100m : Math.Round(consumed, 3);
    }
}
