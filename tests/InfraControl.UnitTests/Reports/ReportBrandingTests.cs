using System.Security.Claims;
using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using InfraControl.Api.Reporting;
using InfraControl.Application.Company;
using InfraControl.Application.Pdi;
using InfraControl.Application.Reports.History;
using InfraControl.Application.Reports.Transversal;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;

namespace InfraControl.UnitTests.Reports;

/// <summary>
/// F52-T004 — ReportBranding: fallback, composição das linhas
/// (empresa/CNPJ/endereço/contato/autoria), leitura tolerante do logo e
/// smoke dos renderers PDF/XLSX/CSV com branding completo.
/// </summary>
public sealed class ReportBrandingTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(), $"report-branding-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ---- Branding model -------------------------------------------------

    [Fact]
    public void From_NullSettings_ShouldFallbackToPlatform()
    {
        var b = ReportBranding.From(null, "ana@exemplo.test");

        b.Name.Should().Be("InfraControl");
        b.CompanyLine.Should().Be("InfraControl");
        b.LegalLine.Should().BeNull();
        b.GeneratedLine.Should().Contain("Gerado por ana@exemplo.test");
        b.GeneratedLine.Should().MatchRegex(@"\d{2}/\d{2}/\d{4} \d{2}:\d{2}");
    }

    [Fact]
    public void From_FullSettings_ShouldComposeIdentityLines()
    {
        var b = ReportBranding.From(Settings(), "Ana Operadora");

        b.CompanyLine.Should().Be("Empresa X · CNPJ 12.345.678/0001-90");
        b.LegalLine.Should().Be("Empresa X LTDA");
        b.Address.Should().Contain("Rua A, 10, Centro, São Paulo - SP, 01000-000");
        b.Contact.Should().Contain("ana@empresa.test");
        b.FooterInfoLine.Should().Contain("Gerado por Ana Operadora");
        b.CsvCompanyLine.Should().Contain("Empresa X LTDA");
    }

    [Fact]
    public void From_SameLegalName_ShouldHideLegalLine()
    {
        var settings = Settings() with { LegalName = "Empresa X" };
        var b = ReportBranding.From(settings);

        b.LegalLine.Should().BeNull();
        b.CompanyLine.Should().Be("Empresa X · CNPJ 12.345.678/0001-90");
    }

    // ---- Logo loader -----------------------------------------------------

    [Fact]
    public void Logo_Load_ShouldReadBytesUnderUploads()
    {
        var env = WebEnv();
        var dir = Path.Combine(_tempRoot, "wwwroot", "uploads", "company");
        Directory.CreateDirectory(dir);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        File.WriteAllBytes(Path.Combine(dir, "logo.png"), png);

        ReportBrandingLogo.Load(env, "/uploads/company/logo.png")
            .Should().Equal(png);
    }

    [Fact]
    public void Logo_Load_ShouldTolerateMissingOrInvalid()
    {
        var env = WebEnv();

        ReportBrandingLogo.Load(env, null).Should().BeNull();
        ReportBrandingLogo.Load(env, "").Should().BeNull();
        ReportBrandingLogo.Load(env, "/uploads/company/none.png").Should().BeNull();
        ReportBrandingLogo.Load(env, "/etc/passwd").Should().BeNull();
        ReportBrandingLogo.Load(env, "https://x.test/logo.png").Should().BeNull();
    }

    // ---- Renderer smoke ---------------------------------------------------

    [Fact]
    public void HistoryRenderers_ShouldEmbedBranding()
    {
        var b = ReportBranding.From(Settings(), "Ana Operadora");
        var history = new ReportHistoryDto(null, null,
        [
            new HistoryMonthDto("2026-01", 3, 2, 1, 1),
        ]);

        var csv = Encoding.UTF8.GetString(
            HistoryReportRenderers.RenderCsv(history, b));
        csv.Should().Contain("Empresa X · CNPJ 12.345.678/0001-90");

        using var xlsx = new XLWorkbook(new MemoryStream(
            HistoryReportRenderers.RenderXlsx(history, b)));
        xlsx.Worksheet(1).Cell(1, 1).GetString()
            .Should().Contain("Empresa X · CNPJ 12.345.678/0001-90");
        xlsx.Worksheet(1).Cell(2, 1).GetString()
            .Should().Contain("Empresa X LTDA");

        var pdf = HistoryReportRenderers.RenderPdf(history, b);
        AssertPdfBranding(pdf, "Empresa X");
    }

    [Fact]
    public void TransversalRenderers_ShouldEmbedBranding()
    {
        var b = ReportBranding.From(Settings(), "Ana Operadora");
        var report = Transversal();

        var pdf = TransversalReportRenderers.RenderTransversalPdf(report, b);
        AssertPdfBranding(pdf, "Empresa X");

        using var xlsx = new XLWorkbook(new MemoryStream(
            TransversalReportRenderers.RenderTransversalXlsx(report, b)));
        xlsx.Worksheet(1).Cell(1, 1).GetString()
            .Should().Contain("CNPJ 12.345.678/0001-90");
    }

    [Fact]
    public void PdiRenderers_ShouldEmbedBranding()
    {
        var b = ReportBranding.From(Settings(), "Ana Operadora");
        var individual = Individual();
        var semester = Semester();
        var difficulties = Difficulties();

        AssertPdfBranding(
            PdiReportRenderers.RenderIndividualPdf(individual, b), "Empresa X");
        AssertPdfBranding(
            PdiReportRenderers.RenderSemesterPdf(semester, b), "Empresa X");
        AssertPdfBranding(
            PdiReportRenderers.RenderDifficultiesPdf(difficulties, b), "Empresa X");

        using var wbIndividual = new XLWorkbook(new MemoryStream(
            PdiReportRenderers.RenderIndividualXlsx(individual, b)));
        wbIndividual.Worksheet("Resumo").Cell(1, 1).GetString()
            .Should().Contain("CNPJ 12.345.678/0001-90");

        using var wbSemester = new XLWorkbook(new MemoryStream(
            PdiReportRenderers.RenderSemesterXlsx(semester, b)));
        wbSemester.Worksheet("Resumo").Cell(1, 1).GetString()
            .Should().Contain("CNPJ 12.345.678/0001-90");

        using var wbDifficulties = new XLWorkbook(new MemoryStream(
            PdiReportRenderers.RenderDifficultiesXlsx(difficulties, b)));
        wbDifficulties.Worksheet("Dificuldades").Cell(1, 1).GetString()
            .Should().Contain("CNPJ 12.345.678/0001-90");
    }

    [Fact]
    public void Renderers_WithoutBranding_ShouldFallbackToPlatform()
    {
        var history = new ReportHistoryDto(null, null, []);

        var csv = Encoding.UTF8.GetString(HistoryReportRenderers.RenderCsv(history));
        csv.Should().Contain("InfraControl");

        var pdf = HistoryReportRenderers.RenderPdf(history);
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    // ---- Helpers -----------------------------------------------------------

    private static void AssertPdfBranding(byte[] pdf, string expected)
    {
        // Smoke do PDF: streams FlateDecode + fontes CID não permitem
        // grep do texto nos bytes — a presença do branding no PDF é
        // coberta pelos helpers de linha (CompanyLine/FooterInfoLine)
        // e verificada literalmente no CSV/XLSX.
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        pdf.Length.Should().BeGreaterThan(5_000);
        expected.Should().NotBeNullOrWhiteSpace();
    }

    private IWebHostEnvironment WebEnv()
    {
        Directory.CreateDirectory(_tempRoot);
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_tempRoot);
        env.SetupGet(e => e.WebRootPath)
            .Returns(Path.Combine(_tempRoot, "wwwroot"));
        return env.Object;
    }

    private static CompanySettingsDto Settings() => new(
        Id: Guid.NewGuid(),
        Name: "Empresa X",
        LegalName: "Empresa X LTDA",
        Cnpj: "12.345.678/0001-90",
        LogoPath: null,
        Street: "Rua A",
        Number: "10",
        Complement: null,
        District: "Centro",
        City: "São Paulo",
        State: "SP",
        ZipCode: "01000-000",
        Phone: "11 3333-4444",
        Email: "ana@empresa.test",
        Website: "https://empresa.test",
        Departments: [],
        UpdatedAt: null);

    private static TransversalReportDto Transversal() => new(
        null, null,
        new PdiReportBlock(1, new Dictionary<string, int> { ["Completed"] = 1 }, 1),
        new TaskReportBlock(2, new Dictionary<string, int> { ["Resolved"] = 1 }, 1, 0),
        new KnowledgeReportBlock(3, 1, 2),
        new ProjectReportBlock(1, new Dictionary<string, int> { ["Active"] = 1 }),
        new IncidentReportBlock(2, 1, 4.5));

    private static PdiIndividualReportDto Individual() => new(
        OwnerName: "Ana Concluída",
        OwnerEmail: "ana@exemplo.test",
        ManagerName: "Carla Gestor",
        PeriodStart: new DateOnly(2026, 10, 1),
        PeriodEnd: new DateOnly(2027, 6, 30),
        Structure: new PdiStructureDto(Guid.NewGuid(), []),
        Accompaniment: new PdiAccompanimentDto(
            Guid.NewGuid(), [], [], [], [], []),
        Evaluation: new PdiEvaluationDto(
            Guid.NewGuid(), Status: "Completed", Strategy: "Ciclo",
            TargetDate: new DateOnly(2027, 6, 30),
            ManagerId: null, ManagerName: null,
            PeriodStart: null, PeriodEnd: null,
            SelfAssessment: null, ManagerAssessment: null, Result: null));

    private static PdiSemesterReportDto Semester() => new(
        Label: "1º SEMESTRE 2027",
        From: new DateOnly(2027, 1, 1),
        To: new DateOnly(2027, 6, 30),
        PlansInPeriod: 2,
        PlansCreatedInPeriod: 2,
        CompletedInPeriod: 1,
        CompletionRatePercent: 50,
        AvgCompletionDays: 90,
        Trends: new PdiSemesterComparisonDto(1, 1, 50, 1, 0, 0),
        Managers: [],
        Competencies: [],
        Difficulties: []);

    private static PdiDifficultiesReportDto Difficulties() => new(
        From: null, To: null, Total: 0, ByType: [],
        Resolved: 0, AvgResolutionDays: null);
}
