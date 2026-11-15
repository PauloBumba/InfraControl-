using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// Relatório geral agendado — regras do ScheduledReport: destinatários,
/// frequência, próximo disparo (horário de Brasília → instante UTC),
/// marcação de envio/falha.
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
            null,
            isActive: true);

        report.Recipients.Should().Equal("Admin@Example.com", "ops@corp.com");
        report.DayOfWeek.Should().BeNull();
        report.NextRunAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithoutRecipients_ShouldThrow()
        => FluentActions.Invoking(() => ScheduledReport.Create(
                [], ReportFrequency.Daily, new TimeOnly(8, 0), null, null, true))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Create_WeeklyWithoutDay_ShouldThrow()
        => FluentActions.Invoking(() => ScheduledReport.Create(
                ["a@b.com"], ReportFrequency.Weekly, new TimeOnly(8, 0), null, null, true))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Create_MonthlyWithoutDay_ShouldThrow()
        => FluentActions.Invoking(() => ScheduledReport.Create(
                ["a@b.com"], ReportFrequency.Monthly, new TimeOnly(8, 0), null, null, true))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void ComputeNextRun_DailyBeforeTime_ReturnsTodayInUtc()
    {
        // 18:00 de Brasília = 21:00 UTC. after=10:00 UTC = 07:00 BR → hoje 18:00 BR.
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(18, 0), null, null, true);

        report.ComputeNextRun(new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 11, 15, 21, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeNextRun_DailyAfterTime_ReturnsTomorrowInUtc()
    {
        // 08:00 BR = 11:00 UTC. after=12:00 UTC = 09:00 BR → amanhã 08:00 BR = 11:00 UTC.
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(8, 0), null, null, true);

        report.ComputeNextRun(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 11, 16, 11, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeNextRun_Weekly_ReturnsNextMatchingWeekdayInUtc()
    {
        // 2026-11-15 é domingo. Weekly segunda 07:00 BR = 10:00 UTC.
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Weekly, new TimeOnly(7, 0), 1, null, true);

        report.ComputeNextRun(new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeNextRun_Monthly_ReturnsNextMonthDayInUtc()
    {
        // Monthly dia 5 às 09:00 BR = 12:00 UTC. after = 2026-11-10 → 2026-12-05.
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Monthly, new TimeOnly(9, 0), null, 5, true);

        report.ComputeNextRun(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 12, 5, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ComputeNextRun_MonthlyDay31_FebruaryUsesLastDay()
    {
        // Dia 31 em fevereiro (2027 não bissexto) → 28/02.
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Monthly, new TimeOnly(9, 0), null, 31, true);

        report.ComputeNextRun(new DateTime(2027, 2, 1, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2027, 2, 28, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void MarkSent_ShouldAdvanceNextRunAndClearError()
    {
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(8, 0), null, null, true);
        report.MarkFailed(DateTime.UtcNow, "smtp down");
        report.LastError.Should().Be("smtp down");

        // Envio às 05:00 BR (08:00 UTC) → próximo ainda hoje 08:00 BR = 11:00 UTC.
        var sentAt = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);
        report.MarkSent(sentAt);

        report.LastSentAtUtc.Should().Be(sentAt);
        report.LastError.Should().BeNull();
        report.NextRunAtUtc.Should().Be(new DateTime(2026, 11, 15, 11, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void MarkFailed_ShouldRetryIn15Minutes()
    {
        var report = ScheduledReport.Create(
            ["a@b.com"], ReportFrequency.Daily, new TimeOnly(8, 0), null, null, true);
        var failedAt = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);

        report.MarkFailed(failedAt, "boom");

        report.NextRunAtUtc.Should().Be(failedAt.AddMinutes(15));
    }
}

/// <summary>Composição do e-mail do relatório geral — seções completas.</summary>
public sealed class GeneralReportComposeTests
{
    private static InfraControl.Application.Reports.Scheduled.GeneralReportContent Content()
        => new(
            new DateTime(2026, 11, 16, 1, 0, 0, DateTimeKind.Utc),
            TotalDevices: 10,
            DevicesByType: new Dictionary<string, int> { ["Router"] = 4, ["Switch"] = 6 },
            OpenIncidents: 2,
            OpenAlerts: 5,
            DiscoveryRunsLast24h: 3,
            DiscoveryRunsFailedLast24h: 1,
            DiscoveryRunsCompletedLast24h: 2,
            CompanyName: "Empresa X",
            CompanyDetails: "CNPJ 00.000.000/0001-00",
            OpenAlertsBySeverity: new Dictionary<string, int> { ["Critical"] = 1, ["Warning"] = 4 },
            IncidentsByStatus: new Dictionary<string, int>
            {
                ["Open"] = 1, ["Investigating"] = 1, ["Resolved"] = 7,
            },
            TasksByState: new Dictionary<string, int> { ["Open"] = 3, ["InProgress"] = 2 },
            SlaViolatedOpenTasks: 1,
            ProjectsByStatus: new Dictionary<string, int> { ["InProgress"] = 2 },
            PdisByStatus: new Dictionary<string, int> { ["Active"] = 4 },
            NotificationDeliveriesLast24h: new Dictionary<string,
                InfraControl.Application.Reports.Scheduled.DeliveryChannelStat>
            {
                ["WhatsApp"] = new(10, 1, 0),
                ["Email"] = new(3, 0, 2),
            });

    [Fact]
    public void ComposeEmail_ShouldIncludeAllSectionsInTextAndHtml()
    {
        var (subject, html, text) =
            InfraControl.Application.Reports.Scheduled.GeneralReportBuilder
                .ComposeEmail(Content());

        subject.Should().Contain("Empresa X");
        text.Should().Contain("MONITORAMENTO").And.Contain("TAREFAS E PROJETOS")
            .And.Contain("NOTIFICAÇÕES");
        text.Should().Contain("Critical: 1").And.Contain("SLA violado");
        text.Should().Contain("WhatsApp: 10 enviadas, 1 falhas");
        html.Should().Contain("Monitoramento").And.Contain("Tarefas e Projetos")
            .And.Contain("Notificações").And.Contain("Pendentes");
    }

    [Fact]
    public void ComposeEmail_EmptyCollections_ShouldRenderPlaceholders()
    {
        var content = Content() with
        {
            TasksByState = new Dictionary<string, int>(),
            ProjectsByStatus = new Dictionary<string, int>(),
            PdisByStatus = new Dictionary<string, int>(),
            NotificationDeliveriesLast24h = new Dictionary<string,
                InfraControl.Application.Reports.Scheduled.DeliveryChannelStat>(),
        };

        var (_, _, text) =
            InfraControl.Application.Reports.Scheduled.GeneralReportBuilder
                .ComposeEmail(content);

        text.Should().Contain("Tasks: nenhuma").And.Contain("Projetos: nenhum")
            .And.Contain("PDIs: nenhum")
            .And.Contain("Nenhuma entrega no período");
    }
}
