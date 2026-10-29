using FluentAssertions;
using InfraControl.Infrastructure.Repositories;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// ADR-049: limites de período dos relatórios devem ser <b>UTC</b> — o
/// Postgres rejeita <c>Kind=Unspecified</c> em <c>timestamptz</c> (bug
/// ao vivo 2026-10-27: todo filtro from/to quebrava só em produção;
/// o SQLite dos testes aceita qualquer Kind e escondia o erro).
/// </summary>
public sealed class ReportPeriodBoundsTests
{
    [Fact]
    public void Lower_É_DiaInício_Em_Utc()
        => ReportPeriodBounds.Between(new DateOnly(2026, 1, 15), null)
            .Lower.Should().Be(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Upper_É_DiaFinal_DoPeríodo_Em_Utc()
    {
        var upper = ReportPeriodBounds.Between(null, new DateOnly(2026, 3, 31)).Upper;

        upper!.Value.Kind.Should().Be(DateTimeKind.Utc);
        upper.Value.Date.Should().Be(new DateTime(2026, 3, 31));
        upper.Value.TimeOfDay.Should().BeGreaterThan(TimeSpan.FromHours(23));
    }

    [Fact]
    public void Ambos_Limites_Tem_Kind_Utc()
    {
        var (lower, upper) = ReportPeriodBounds.Between(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        lower!.Value.Kind.Should().Be(DateTimeKind.Utc);
        upper!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Sem_Periodo_Devolve_Nulos()
    {
        var (lower, upper) = ReportPeriodBounds.Between(null, null);

        lower.Should().BeNull();
        upper.Should().BeNull();
    }

    [Fact]
    public void Só_Inferior_Devolve_Superior_Nulo()
    {
        var (lower, upper) = ReportPeriodBounds.Between(new DateOnly(2026, 6, 1), null);

        lower.Should().NotBeNull();
        upper.Should().BeNull();
    }
}
