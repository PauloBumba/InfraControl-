using System.Text;
using FluentAssertions;
using InfraControl.Api.Reporting;
using InfraControl.Application.Pdi;
using InfraControl.Application.Pdi.Commands;
using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Pdi.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Pdi;

/// <summary>
/// Fase 8 do PDI (docs/30 §21, §22, ADR-038): analytics — indicadores
/// por período, relatórios individual/semestral/dificuldades e
/// exportação PDF/Excel (renderers unit-tested).
/// </summary>
public sealed class PdiAnalyticsTests
{
    private static readonly DateOnly Today =
        DateOnly.FromDateTime(DateTime.UtcNow);

    // ---- §21 Indicadores --------------------------------------------

    [Fact]
    public async Task Indicators_WithoutFilter_ShouldAggregateAllPlans()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var dto = await IndicatorsQuery(context, scene.Admin).Handle(
            new GetPdiIndicatorsQuery(), CancellationToken.None);

        dto.Execution.TotalPlans.Should().Be(3);
        dto.Execution.CompletedPlans.Should().Be(1);
        dto.Execution.CompletionRatePercent.Should().Be(33, "1 de 3");
        dto.Execution.OverduePlans.Should().Be(1, "meta vencida em curso");
        dto.Execution.PhasesCompleted.Should().Be(1);
        dto.Execution.PhasesOverdue.Should().Be(1, "fase com prazo vencido");
        dto.Execution.AvgCompletionDays.Should().NotBeNull()
            .And.BeGreaterThanOrEqualTo(0);

        dto.Development.ObjectivesTotal.Should().Be(2);
        dto.Development.CompetenciesWorked.Should().Be(1);
        dto.Development.ResultsAtingido.Should().Be(1);
        dto.Development.ResultsParcial.Should().Be(0);
        var evolution = dto.Development.EvolutionByCompetency.Should()
            .ContainSingle().Subject;
        evolution.Name.Should().Be("liderança");
        evolution.Objectives.Should().Be(1);
        evolution.AvgPhasePercent.Should().Be(75, "(100 + 50) / 2");

        dto.Accompaniment.CheckIns.Should().Be(2);
        dto.Accompaniment.CheckInsPerPlan.Should().BeApproximately(
            0.7, 0.01, "2/3");
        dto.Accompaniment.Feedbacks.Should().Be(1);
        dto.Accompaniment.EvidencesValidated.Should().Be(1);

        dto.Difficulties.TotalImpediments.Should().Be(2);
        var byType = dto.Difficulties.ByType.ToDictionary(
            entry => entry.Type, entry => entry.Percent);
        byType.Should().ContainKey("Recurso").WhoseValue.Should().Be(50);
        byType.Should().ContainKey("Horário").WhoseValue.Should().Be(50);
        dto.Difficulties.AvgResolutionDays.Should().Be(20,
            "ocorrido há 20 dias, resolvido hoje");
    }

    [Fact]
    public async Task Indicators_PeriodFilter_ShouldScopePlans()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        await SeedAnalyticsAsync(context);
        var admin = await SeedUserAsync(
            context, "admin-ind@exemplo.test", "Ada Admin", UserRole.Admin);
        var adminActor = new FakeCurrentUser(admin.Id, "Admin");

        var dto = await IndicatorsQuery(context, adminActor).Handle(
            new GetPdiIndicatorsQuery(
                Today.AddDays(-15), Today.AddDays(-5)),
            CancellationToken.None);

        dto.Execution.TotalPlans.Should().Be(1, "só a meta em 2026 (T-10)");
        dto.Execution.CompletedPlans.Should().Be(0);
        dto.Execution.OverduePlans.Should().Be(1);
        dto.Accompaniment.CheckIns.Should().Be(0,
            "os check-ins estão no plano fora do período");
        dto.Difficulties.TotalImpediments.Should().Be(0);
    }

    [Fact]
    public async Task Indicators_ByNonAdmin_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var act = () => IndicatorsQuery(context, scene.Owner).Handle(
            new GetPdiIndicatorsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Indicators_EmptyState_ShouldReturnZeros()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var admin = await SeedUserAsync(
            context, "vazio@exemplo.test", "Vazio", UserRole.Admin);
        var actor = new FakeCurrentUser(admin.Id, "Admin");

        var dto = await IndicatorsQuery(context, actor).Handle(
            new GetPdiIndicatorsQuery(), CancellationToken.None);

        dto.Execution.TotalPlans.Should().Be(0);
        dto.Execution.CompletionRatePercent.Should().Be(0);
        dto.Execution.AvgCompletionDays.Should().BeNull();
        dto.Development.EvolutionByCompetency.Should().BeEmpty();
        dto.Accompaniment.CheckInsPerPlan.Should().Be(0);
        dto.Difficulties.ByType.Should().BeEmpty();
        dto.Difficulties.AvgResolutionDays.Should().BeNull();
    }

    [Fact]
    public async Task Indicators_FromAfterTo_ShouldFail400()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var act = () => IndicatorsQuery(context, scene.Admin).Handle(
            new GetPdiIndicatorsQuery(Today, Today.AddDays(-10)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*período*");
    }

    // ---- §22 Relatório individual ------------------------------------

    [Fact]
    public async Task IndividualReport_ShouldIncludeAllSections()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var dto = await IndividualQuery(context, scene.Admin).Handle(
            new GetPdiIndividualReportQuery(scene.Owner1Id),
            CancellationToken.None);

        dto.OwnerName.Should().Be("Ana Concluída");
        dto.ManagerName.Should().Be("Carla Gestor");
        dto.Structure.Objectives.Should().HaveCount(1);
        dto.Structure.Objectives[0].Strategies.Should().HaveCount(1);
        dto.Structure.Objectives[0].Strategies[0].Phases.Should().HaveCount(2);
        dto.Accompaniment.CheckIns.Should().HaveCount(2);
        dto.Accompaniment.Impediments.Should().HaveCount(2);
        dto.Accompaniment.Evidences.Should().HaveCount(1);
        dto.Accompaniment.Feedbacks.Should().HaveCount(1);
        dto.Evaluation.SelfAssessment.Should().NotBeNull();
        dto.Evaluation.ManagerAssessment.Should().NotBeNull();
        dto.Evaluation.Result.Should().NotBeNull();
        dto.Evaluation.Result!.Outcome.Should().Be("Atingido");
        dto.Evaluation.Status.Should().Be(nameof(PdiStatus.Completed));
        dto.PeriodStart.Should().NotBeNull();
    }

    [Fact]
    public async Task IndividualReport_ByStranger_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var act = () => IndividualQuery(context, scene.Stranger).Handle(
            new GetPdiIndividualReportQuery(scene.Owner1Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task IndividualReport_WithoutPlan_ShouldFail404()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);
        var orphan = await SeedUserAsync(
            context, "sem-plano-rel@exemplo.test", "Sem Plano");

        var act = () => IndividualQuery(context, scene.Admin).Handle(
            new GetPdiIndividualReportQuery(orphan.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---- §22 Relatório semestral -------------------------------------

    [Fact]
    public async Task SemesterReport_ShouldSummarizePeriod()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        await SeedAnalyticsAsync(context);
        var admin = await SeedUserAsync(
            context, "admin-sem@exemplo.test", "Ada Admin", UserRole.Admin);
        var actor = new FakeCurrentUser(admin.Id, "Admin");

        var dto = await SemesterQuery(context, actor).Handle(
            new GetPdiSemesterReportQuery(
                Today.AddDays(-30), Today.AddDays(30)),
            CancellationToken.None);

        dto.PlansInPeriod.Should().Be(2, "planos com prazo na janela");
        dto.CompletedInPeriod.Should().Be(1);
        dto.CompletionRatePercent.Should().Be(50);
        dto.PlansCreatedInPeriod.Should().Be(3, "criados agora");
        dto.AvgCompletionDays.Should().NotBeNull();

        var manager = dto.Managers.Should().ContainSingle(
            entry => entry.ManagerName == "Carla Gestor").Subject;
        manager.Plans.Should().Be(2);
        manager.Completed.Should().Be(1);

        var competency = dto.Competencies.Should().ContainSingle().Subject;
        competency.Name.Should().Be("liderança");
        competency.AvgPhasePercent.Should().Be(75);

        dto.Difficulties.Should().HaveCount(2);
        dto.Trends.PreviousPlansInPeriod.Should().Be(0,
            "janela anterior sem planos");
        dto.Trends.PlansDelta.Should().Be(2);
        dto.Trends.CompletedDelta.Should().Be(1);
        dto.Trends.CompletionRateDelta.Should().Be(50);
        dto.Label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SemesterReport_DefaultWindow_ShouldUseCurrentSemester()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        await SeedAnalyticsAsync(context);
        var admin = await SeedUserAsync(
            context, "admin-sem2@exemplo.test", "Ada Admin", UserRole.Admin);
        var actor = new FakeCurrentUser(admin.Id, "Admin");

        // Metas do seed (relativas a hoje) — expectativas derivadas da
        // janela para o teste não depender da data da execução.
        var targets = new[]
        {
            Today.AddDays(-10), Today.AddDays(30), Today.AddDays(45)
        };

        var current = await SemesterQuery(context, actor).Handle(
            new GetPdiSemesterReportQuery(), CancellationToken.None);
        var expected = $"{(Today.Month <= 6 ? 1 : 2)}º SEMESTRE {Today.Year}";
        var currentStart = Today.Month <= 6
            ? new DateOnly(Today.Year, 1, 1)
            : new DateOnly(Today.Year, 7, 1);
        var currentEnd = Today.Month <= 6
            ? new DateOnly(Today.Year, 6, 30)
            : new DateOnly(Today.Year, 12, 31);
        current.Label.Should().Be(expected);
        current.PlansInPeriod.Should().Be(
            targets.Count(target =>
                target >= currentStart && target <= currentEnd));

        var named = await SemesterQuery(context, actor).Handle(
            new GetPdiSemesterReportQuery(
                new DateOnly(2027, 1, 1), new DateOnly(2027, 6, 30)),
            CancellationToken.None);
        named.Label.Should().Be("1º SEMESTRE 2027");
        named.PlansInPeriod.Should().Be(
            targets.Count(target =>
                target >= new DateOnly(2027, 1, 1)
                && target <= new DateOnly(2027, 6, 30)));
        // Janela anterior = [start - tamanho..start-1] calculada à parte.
        var previousTo = new DateOnly(2026, 12, 31);
        var previousFrom = previousTo.AddDays(-180);
        named.Trends.PreviousPlansInPeriod.Should().Be(
            targets.Count(target =>
                target >= previousFrom && target <= previousTo));
    }

    [Fact]
    public async Task SemesterReport_ByNonAdmin_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var act = () => SemesterQuery(context, scene.Owner).Handle(
            new GetPdiSemesterReportQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---- §22 Relatório de dificuldades --------------------------------

    [Fact]
    public async Task DifficultiesReport_ShouldDistributeByType()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var dto = await DifficultiesQuery(context, scene.Admin).Handle(
            new GetPdiDifficultiesReportQuery(), CancellationToken.None);

        dto.Total.Should().Be(2);
        dto.Resolved.Should().Be(1);
        dto.AvgResolutionDays.Should().Be(20);
        var byType = dto.ByType.ToDictionary(
            entry => entry.Type, entry => entry.Percent);
        byType.Should().ContainKey("Recurso").WhoseValue.Should().Be(50);
        byType.Should().ContainKey("Horário").WhoseValue.Should().Be(50);
        dto.ByType.Sum(entry => entry.Count).Should().Be(dto.Total);
    }

    [Fact]
    public async Task DifficultiesReport_ByNonAdmin_ShouldFail403()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedAnalyticsAsync(context);

        var act = () => DifficultiesQuery(context, scene.Stranger).Handle(
            new GetPdiDifficultiesReportQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---- Filtro de período do dashboard (adiado na Fase 7) -----------

    [Fact]
    public async Task AdminDashboard_PeriodFilter_ShouldScopePlans()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        await SeedAnalyticsAsync(context);
        var admin = await SeedUserAsync(
            context, "admin-dash8@exemplo.test", "Ada Admin", UserRole.Admin);
        var actor = new FakeCurrentUser(admin.Id, "Admin");

        var all = await AdminDashboardQuery(context, actor).Handle(
            new GetPdiAdminDashboardQuery(), CancellationToken.None);
        all.TotalPlans.Should().Be(3);

        var scoped = await AdminDashboardQuery(context, actor).Handle(
            new GetPdiAdminDashboardQuery(
                Today.AddDays(-15), Today.AddDays(-5)),
            CancellationToken.None);
        scoped.TotalPlans.Should().Be(1, "só a meta com prazo na janela");
        scoped.InProgressPlans.Should().Be(1);
    }

    // ---- Renderers (PDF/Excel) ----------------------------------------

    [Fact]
    public void Renderers_ShouldProducePdfAndXlsx()
    {
        var individual = SampleIndividual();
        var semester = SampleSemester();
        var difficulties = SampleDifficulties();

        var pdf = PdiReportRenderers.RenderIndividualPdf(individual);
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        PdiReportRenderers.RenderSemesterPdf(semester)[..4].Should()
            .Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');
        PdiReportRenderers.RenderDifficultiesPdf(difficulties)[..4].Should()
            .Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');

        var xlsx = PdiReportRenderers.RenderIndividualXlsx(individual);
        AssertWorkbook(xlsx, "Resumo");
        AssertWorkbook(
            PdiReportRenderers.RenderSemesterXlsx(semester), "Resumo");
        AssertWorkbook(
            PdiReportRenderers.RenderDifficultiesXlsx(difficulties),
            "Dificuldades");
    }

    [Fact]
    public void ExportFormat_Parse_ShouldAcceptPdfAndXlsxOnly()
    {
        PdiExportFormat.Parse("pdf").Should().Be(PdiExportFormat.Pdf);
        PdiExportFormat.Parse("XLSX").Should().Be(PdiExportFormat.Xlsx);
        PdiExportFormat.Parse(null).Should().Be(PdiExportFormat.Pdf,
            "padrão PDF");

        var act = () => PdiExportFormat.Parse("docx");
        act.Should().Throw<ArgumentException>().WithMessage("*formato*");
    }

    private static void AssertWorkbook(byte[] bytes, string expectedSheet)
    {
        bytes[0].Should().Be((byte)'P', "assinatura ZIP do xlsx");
        bytes[1].Should().Be((byte)'K');
        using var stream = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        workbook.Worksheets.Select(sheet => sheet.Name)
            .Should().Contain(expectedSheet);
    }

    private static PdiIndividualReportDto SampleIndividual()
        => new(
            OwnerName: "Ana Concluída",
            OwnerEmail: "ana@exemplo.test",
            ManagerName: "Carla Gestor",
            PeriodStart: new DateOnly(2026, 10, 1),
            PeriodEnd: new DateOnly(2027, 6, 30),
            Structure: new PdiStructureDto(
                Guid.NewGuid(),
                [
                    Objective(
                        "Assumir liderança", "liderança",
                        Strategies:
                        [
                            Strategy(
                                "Formação em gestão",
                                [
                                    Phase("Capacitação", "Completed", 100,
                                        [Activity("Curso", false)]),
                                    Phase("Aplicação", "InProgress", 50, []),
                                ]),
                        ]),
                ]),
            Accompaniment: new PdiAccompanimentDto(
                Guid.NewGuid(),
                Progresses: [],
                CheckIns:
                [
                    CheckIn(new DateOnly(2027, 2, 10), "Curso concluído"),
                ],
                Impediments:
                [
                    Impediment("Recurso", "Servidor fora do ar",
                        new DateOnly(2027, 1, 5), "Open", null),
                ],
                Feedbacks:
                [
                    Feedback("Reconhecimento", "Bom trabalho no ciclo.",
                        new DateOnly(2027, 3, 1)),
                ],
                Evidences:
                [
                    Evidence("certificate.pdf", "Validated",
                        new DateTime(2027, 2, 11, 12, 0, 0, DateTimeKind.Utc)),
                ]),
            Evaluation: new PdiEvaluationDto(
                Guid.NewGuid(),
                Status: nameof(PdiStatus.Completed),
                Strategy: "Ciclo de liderança",
                TargetDate: new DateOnly(2027, 6, 30),
                ManagerId: Guid.NewGuid(),
                ManagerName: "Carla Gestor",
                PeriodStart: new DateOnly(2026, 10, 1),
                PeriodEnd: new DateOnly(2027, 6, 30),
                SelfAssessment: new PdiAssessmentDto(
                    Guid.NewGuid(), Guid.NewGuid(), "Self", Guid.NewGuid(),
                    "Ana Concluída", "Autoavaliação do ciclo.",
                    DateTime.UtcNow, DateTime.UtcNow),
                ManagerAssessment: new PdiAssessmentDto(
                    Guid.NewGuid(), Guid.NewGuid(), "Manager", Guid.NewGuid(),
                    "Carla Gestor", "Avaliação do gestor.",
                    DateTime.UtcNow, DateTime.UtcNow),
                Result: new PdiResultDto(
                    Guid.NewGuid(), Guid.NewGuid(), "Atingido",
                    "Meta alcançada.", Guid.NewGuid(), "Carla Gestor",
                    new DateOnly(2027, 6, 20))));

    private static PdiSemesterReportDto SampleSemester()
        => new(
            Label: "1º SEMESTRE 2027",
            From: new DateOnly(2027, 1, 1),
            To: new DateOnly(2027, 6, 30),
            PlansInPeriod: 12,
            PlansCreatedInPeriod: 14,
            CompletedInPeriod: 8,
            CompletionRatePercent: 67,
            AvgCompletionDays: 120,
            Trends: new PdiSemesterComparisonDto(
                PreviousPlansInPeriod: 10,
                PreviousCompleted: 5,
                PreviousCompletionRatePercent: 50,
                PlansDelta: 2,
                CompletedDelta: 3,
                CompletionRateDelta: 17),
            Managers:
            [
                new PdiSemesterManagerDto("Carla Gestor", Plans: 6,
                    Completed: 4),
            ],
            Competencies:
            [
                new PdiSemesterCompetencyDto("liderança", Objectives: 5,
                    AvgPhasePercent: 78),
            ],
            Difficulties:
            [
                new PdiDifficultyShareDto("Recurso", Count: 9, Percent: 60),
                new PdiDifficultyShareDto("Horário", Count: 6, Percent: 40),
            ]);

    private static PdiDifficultiesReportDto SampleDifficulties()
        => new(
            From: null,
            To: null,
            Total: 15,
            ByType:
            [
                new PdiDifficultyShareDto("Recurso", Count: 9, Percent: 60),
                new PdiDifficultyShareDto("Horário", Count: 6, Percent: 40),
            ],
            Resolved: 12,
            AvgResolutionDays: 8);

    private static PdiObjectiveNodeDto Objective(
        string title, string? competencyName,
        PdiStrategyNodeDto[] Strategies)
        => new(Guid.NewGuid(), title, Description: null,
            CompetencyId: competencyName is null ? null : Guid.NewGuid(),
            CompetencyName: competencyName, CurrentSituation: null,
            DesiredSituation: null, ExpectedResult: null, Indicator: null,
            TargetDate: null, Order: 1)
        { Strategies = Strategies };

    private static PdiStrategyNodeDto Strategy(
        string title, PdiPhaseNodeDto[] Phases)
        => new(Guid.NewGuid(), title, Description: null, Order: 1)
        { Phases = Phases };

    private static PdiPhaseNodeDto Phase(
        string title, string status, int percent,
        PdiActivityNodeDto[] Activities)
        => new(Guid.NewGuid(), title, Description: null, ResponsibleId: null,
            ResponsibleName: null, StartDate: null, EndDate: null,
            CompletionCriteria: null, Status: status, Percent: percent,
            Order: 1)
        { Activities = Activities };

    private static PdiActivityNodeDto Activity(string title, bool completed)
        => new(Guid.NewGuid(), title, DueDate: null, completed, Order: 1);

    private static PdiCheckInDto CheckIn(DateOnly date, string whatDone)
        => new(Guid.NewGuid(), Guid.NewGuid(), date, Guid.NewGuid(),
            "Ana Concluída", whatDone, null, null, null, null, null, null,
            null);

    private static PdiImpedimentDto Impediment(
        string type, string description, DateOnly occurredAt,
        string status, DateOnly? resolvedAt)
        => new(Guid.NewGuid(), Guid.NewGuid(), type, description,
            Impact: null, occurredAt, ResponsibleId: null,
            ResponsibleName: null, ActionTaken: null, status, resolvedAt);

    private static PdiFeedbackDto Feedback(
        string type, string content, DateOnly date)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Carla Gestor", Guid.NewGuid(), "Ana Concluída", type, content,
            date);

    private static PdiEvidenceDto Evidence(
        string fileName, string status, DateTime createdAt)
        => new(Guid.NewGuid(), Guid.NewGuid(), "Certificate", status,
            Description: null, fileName, "application/pdf", 1024,
            Guid.NewGuid(), "Ana Concluída", createdAt, null, null, null,
            null);

    // ---- Fixtures -----------------------------------------------------

    private sealed record AnalyticsScene(
        FakeCurrentUser Admin,
        FakeCurrentUser Owner,
        FakeCurrentUser Stranger,
        Guid Owner1Id);

    private static async Task<AnalyticsScene> SeedAnalyticsAsync(
        AppDbContext context)
    {
        var admin = await SeedUserAsync(
            context, "ada@exemplo.test", "Ada Admin", UserRole.Admin);
        var manager = await SeedUserAsync(
            context, "carla@exemplo.test", "Carla Gestor");
        var owner1 = await SeedUserAsync(
            context, "ana@exemplo.test", "Ana Concluída");
        var owner2 = await SeedUserAsync(
            context, "bia@exemplo.test", "Bia Atraso");
        var owner3 = await SeedUserAsync(
            context, "caio@exemplo.test", "Caio Replanejado");
        var stranger = await SeedUserAsync(
            context, "estranho@exemplo.test", "Estranho");

        var adminActor = new FakeCurrentUser(admin.Id, "Admin");
        var owner1Actor = new FakeCurrentUser(owner1.Id, "Operator");

        // Plano concluído (criação → InProgress → UnderEvaluation → Completed).
        var plan1 = IndividualDevelopmentPlan.Create(
            owner1.Id, "Plano concluído", Today.AddDays(30),
            PdiStatus.InProgress, manager.Id,
            new DateOnly(2026, 10, 1), new DateOnly(2027, 6, 30),
            new PdiChangeActor(owner1.Id, owner1.Email, owner1.Email));
        plan1.Update(
            new PdiChanges(plan1.Strategy, plan1.TargetDate,
                PdiStatus.UnderEvaluation, plan1.ManagerId,
                plan1.PeriodStart, plan1.PeriodEnd, Reason: null),
            new PdiChangeActor(owner1.Id, owner1.Email, owner1.Email));
        plan1.Update(
            new PdiChanges(plan1.Strategy, plan1.TargetDate,
                PdiStatus.Completed, plan1.ManagerId,
                plan1.PeriodStart, plan1.PeriodEnd, Reason: null),
            new PdiChangeActor(owner1.Id, owner1.Email, owner1.Email));
        context.IndividualDevelopmentPlans.Add(plan1);
        context.PdiHistoryEntries.AddRange(plan1.DrainPendingHistory());

        var plan2 = IndividualDevelopmentPlan.Create(
            owner2.Id, "Plano atrasado", Today.AddDays(-10),
            PdiStatus.Active, manager.Id, null, null,
            new PdiChangeActor(owner2.Id, owner2.Email, owner2.Email));
        context.IndividualDevelopmentPlans.Add(plan2);
        context.PdiHistoryEntries.AddRange(plan2.DrainPendingHistory());

        var plan3 = IndividualDevelopmentPlan.Create(
            owner3.Id, "Plano replanejado", Today.AddDays(45),
            PdiStatus.Replanned, manager.Id, null, null,
            new PdiChangeActor(owner3.Id, owner3.Email, owner3.Email));
        context.IndividualDevelopmentPlans.Add(plan3);
        context.PdiHistoryEntries.AddRange(plan3.DrainPendingHistory());
        await context.SaveChangesAsync();

        // Catálogo + estrutura do plano concluído (§21 desenvolvimento).
        var competency = await CompetencyCreate(context, adminActor).Handle(
            new CreatePdiCompetencyCommand("liderança"), CancellationToken.None);
        var objective1 = await ObjectiveCreate(context, adminActor).Handle(
            new CreatePdiObjectiveCommand(
                owner1.Id, "Assumir liderança",
                CompetencyId: competency.Id),
            CancellationToken.None);
        var strategy1 = await StrategyCreate(context, adminActor).Handle(
            new CreatePdiStrategyCommand(objective1.Id, "Formação"),
            CancellationToken.None);
        var phaseA = await PhaseCreate(context, adminActor).Handle(
            new CreatePdiPhaseCommand(strategy1.Id, "Capacitação",
                Status: nameof(PdiPhaseStatus.Completed), Percent: 100),
            CancellationToken.None);
        var phaseB = await PhaseCreate(context, adminActor).Handle(
            new CreatePdiPhaseCommand(strategy1.Id, "Aplicação",
                Status: nameof(PdiPhaseStatus.InProgress), Percent: 50),
            CancellationToken.None);

        // Estrutura do plano atrasado (fase com prazo vencido).
        var objective2 = await ObjectiveCreate(context, adminActor).Handle(
            new CreatePdiObjectiveCommand(owner2.Id, "Melhorar comunicação"),
            CancellationToken.None);
        var strategy2 = await StrategyCreate(context, adminActor).Handle(
            new CreatePdiStrategyCommand(objective2.Id, "Comunicação"),
            CancellationToken.None);
        await PhaseCreate(context, adminActor).Handle(
            new CreatePdiPhaseCommand(strategy2.Id, "Treinamento atrasado",
                EndDate: Today.AddDays(-5)),
            CancellationToken.None);

        // Acompanhamento do plano concluído.
        await CheckInCreate(context, owner1Actor).Handle(
            new CreatePdiCheckInCommand(
                phaseA.Id, Today.AddDays(-15), "Módulo 1 concluído"),
            CancellationToken.None);
        await CheckInCreate(context, owner1Actor).Handle(
            new CreatePdiCheckInCommand(
                phaseB.Id, Today.AddDays(-7), "Mentoria aplicada"),
            CancellationToken.None);

        var impediment1 = await ImpedimentCreate(context, owner1Actor).Handle(
            new CreatePdiImpedimentCommand(
                phaseA.Id, "Recurso", "Servidor indisponível",
                Today.AddDays(-20)),
            CancellationToken.None);
        await ImpedimentUpdate(context, adminActor).Handle(
            new UpdatePdiImpedimentCommand(
                impediment1.Id, "Recurso", "Servidor indisponível",
                Today.AddDays(-20), Status: "Resolved"),
            CancellationToken.None);
        await ImpedimentCreate(context, owner1Actor).Handle(
            new CreatePdiImpedimentCommand(
                phaseA.Id, "Horário", "Conflito de agenda",
                Today.AddDays(-3)),
            CancellationToken.None);

        await EvidenceUpload(context, owner1Actor, new FakeFileStore()).Handle(
            new UploadPdiEvidenceCommand(
                phaseA.Id, PdiEvidenceType.Certificate, "Certificado",
                "certificado.pdf", "application/pdf",
                FileSize: 8, Content: new MemoryStream(new byte[8])),
            CancellationToken.None);
        var evidence = (await context.PdiEvidences.SingleAsync());
        await EvidenceReview(context, adminActor).Handle(
            new ReviewPdiEvidenceCommand(
                evidence.Id, PdiEvidenceReviewDecision.Validated, null),
            CancellationToken.None);

        await FeedbackCreate(context, owner1Actor).Handle(
            new CreatePdiFeedbackCommand(
                phaseA.Id, owner1.Id, "Reconhecimento",
                "Bom trabalho no ciclo.", Today.AddDays(-5)),
            CancellationToken.None);

        // Avaliação final + resultado do plano concluído.
        context.PdiAssessments.Add(PdiAssessment.Create(
            plan1.Id, PdiAssessmentSide.Self, owner1.Id,
            "Autoavaliação do ciclo."));
        context.PdiAssessments.Add(PdiAssessment.Create(
            plan1.Id, PdiAssessmentSide.Manager, manager.Id,
            "Avaliação do gestor."));
        context.PdiResults.Add(PdiResult.Create(
            plan1.Id, PdiResultOutcome.Atingido, "Meta alcançada.",
            admin.Id));
        await context.SaveChangesAsync();

        return new AnalyticsScene(
            adminActor,
            new FakeCurrentUser(owner1.Id, "Operator"),
            new FakeCurrentUser(stranger.Id, "Operator"),
            owner1.Id);
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext context, string email, string displayName,
        UserRole? role = null)
    {
        var user = User.Create(email, displayName);
        if (role is { } explicitRole)
            user.AssignRole(explicitRole);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static GetPdiIndicatorsQueryHandler IndicatorsQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user);

    private static GetPdiIndividualReportQueryHandler IndividualQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user);

    private static GetPdiSemesterReportQueryHandler SemesterQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user);

    private static GetPdiDifficultiesReportQueryHandler DifficultiesQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user);

    private static GetPdiAdminDashboardQueryHandler AdminDashboardQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiRepository(context), new PdiStructureRepository(context),
            user);

    private static CreatePdiCompetencyCommandHandler CompetencyCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context),
            NullLogger<CreatePdiCompetencyCommandHandler>.Instance);

    private static CreatePdiObjectiveCommandHandler ObjectiveCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiObjectiveCommandHandler>.Instance);

    private static CreatePdiStrategyCommandHandler StrategyCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiStrategyCommandHandler>.Instance);

    private static CreatePdiPhaseCommandHandler PhaseCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiPhaseCommandHandler>.Instance);

    private static CreatePdiCheckInCommandHandler CheckInCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiCheckInCommandHandler>.Instance);

    private static CreatePdiImpedimentCommandHandler ImpedimentCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiImpedimentCommandHandler>.Instance);

    private static UpdatePdiImpedimentCommandHandler ImpedimentUpdate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<UpdatePdiImpedimentCommandHandler>.Instance);

    private static UploadPdiEvidenceCommandHandler EvidenceUpload(
        AppDbContext context, FakeCurrentUser user, FakeFileStore store)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, store, NullLogger<UploadPdiEvidenceCommandHandler>.Instance);

    private static ReviewPdiEvidenceCommandHandler EvidenceReview(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<ReviewPdiEvidenceCommandHandler>.Instance);

    private static CreatePdiFeedbackCommandHandler FeedbackCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context),
            user, NullLogger<CreatePdiFeedbackCommandHandler>.Instance);

    private sealed class FakeFileStore : IPdiEvidenceFileStore
    {
        public Task SaveAsync(
            Guid evidenceId, string extension, Stream content,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Stream> OpenReadAsync(
            Guid evidenceId, string extension,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(
            Guid evidenceId, string extension,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCurrentUser(Guid? userId, params string[] roles)
        : ICurrentUserContext
    {
        public Guid? UserId { get; } = userId;
        public Guid Id => UserId!.Value;
        public string? UserName => $"ator-{UserId}@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = roles;
    }
}
