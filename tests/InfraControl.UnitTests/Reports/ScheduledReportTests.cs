using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// Relatório geral agendado — regras do ScheduledReport: destinatários,
/// frequência, próximo disparo UTC, marcação de envio/falha.
/// </summary>
public sealed class ScheduledReportTests
{
    [Fact]
    public void Create_ShouldNormalizeRecipientsAndComputeNextDailyRun()
    {
        var report = ScheduledReport.Create(
            ["  Admin@Example.com ", "admin@example.com", "ops@corp.com"],
            ReportFrequency.Daily,
            new TimeOnly(8, 30),
            null,
            isActive: true);

        report.Recipients.Should().Equal("Admin@Example.com", "ops@corp.com");
        report.DayOfWeek.Should().BeNull();
        report.NextRunAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithoutRecipients_ShouldThrow()
        => FluentActions.Invoking(() => ScheduledReport.Create(
                [], ReportFrequency.Daily, new TimeOnly(8, 0), null, true))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Create_WeeklyWithoutDay_ShouldThrow()
        => FluentActions.Invoking(() => ScheduledReport.Create(
                ["a@b.com"], ReportFrequency.Weekly, new TimeOnly(8, 0), null, true))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void ComputeNextRun_DailyBeforeTime_ReturnsToday()
    {
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(18, 0), null, true);

        // 10:00 UTC < 18:00 → hoje às 18:00
        report.ComputeNextRun(new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 11, 15, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeNextRun_DailyAfterTime_ReturnsTomorrow()
    {
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(8, 0), null, true);

        report.ComputeNextRun(new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeNextRun_Weekly_ReturnsNextMatchingWeekday()
    {
        // 2026-11-15 é domingo (DayOfWeek=0). Weekly segunda (1) às 07:00.
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Weekly, new TimeOnly(7, 0), 1, true);

        report.ComputeNextRun(new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 11, 16, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void MarkSent_ShouldAdvanceNextRunAndClearError()
    {
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(8, 0), null, true);
        report.MarkFailed(DateTime.UtcNow, "smtp down");
        report.LastError.Should().Be("smtp down");

        var sentAt = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);
        report.MarkSent(sentAt);

        report.LastSentAtUtc.Should().Be(sentAt);
        report.LastError.Should().BeNull();
        report.NextRunAtUtc.Should().Be(new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void MarkFailed_ShouldRetryIn15Minutes()
    {
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(8, 0), null, true);
        var failedAt = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);

        report.MarkFailed(failedAt, "boom");

        report.NextRunAtUtc.Should().Be(failedAt.AddMinutes(15));
    }
}
