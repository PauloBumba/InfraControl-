using FluentAssertions;
using InfraControl.Application.Reports.Automatics;
using InfraControl.Application.Reports.Automatics.Queries;
using InfraControl.Domain.Common;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// F25-T006 §15 — cadência e janela dos relatórios automáticos
/// (docs/33 §15). Testa <c>ReportCadenceCalculator</c> e a query de geração.
/// </summary>
public sealed class AutomaticReportTests
{
    [Theory]
    [InlineData("mensal", ReportCadence.Mensal)]
    [InlineData("TRIMESTRAL", ReportCadence.Trimestral)]
    [InlineData("semestral", ReportCadence.Semestral)]
    [InlineData("anual", ReportCadence.Anual)]
    public void Parse_ShouldMapValidCadence(string value, ReportCadence expected)
        => ReportCadenceCalculator.Parse(value).Should().Be(expected);

    [Fact]
    public void Parse_Invalid_ShouldThrow()
    {
        var act = () => ReportCadenceCalculator.Parse("semanal");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Window_ShouldComputePeriodStarts()
    {
        var today = new DateOnly(2026, 8, 15);

        var (mensalFrom, mensalTo) = ReportCadenceCalculator.Window(
            ReportCadence.Mensal, today);
        mensalFrom.Should().Be(new DateOnly(2026, 8, 1));
        mensalTo.Should().Be(today);

        var (triFrom, _) = ReportCadenceCalculator.Window(
            ReportCadence.Trimestral, today);
        triFrom.Should().Be(new DateOnly(2026, 7, 1), "3º trimestre começa em jul");

        var (semFrom, _) = ReportCadenceCalculator.Window(
            ReportCadence.Semestral, today);
        semFrom.Should().Be(new DateOnly(2026, 7, 1), "2º semestre começa em jul");

        var (anualFrom, _) = ReportCadenceCalculator.Window(
            ReportCadence.Anual, today);
        anualFrom.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public async Task AutomaticReport_ShouldUseCadenceWindow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var handler = new GetAutomaticReportQueryHandler(
            new ModuleReportsRepository(context));

        var result = await handler.Handle(
            new GetAutomaticReportQuery("anual"), CancellationToken.None);

        var today = DateOnly.FromDateTime(
            AppTimeProvider.Current.GetUtcNow().UtcDateTime.Date);
        result.Cadence.Should().Be("anual");
        result.From.Should().Be(new DateOnly(today.Year, 1, 1));
        result.To.Should().Be(today);
        result.Report.Should().NotBeNull();
    }
}
