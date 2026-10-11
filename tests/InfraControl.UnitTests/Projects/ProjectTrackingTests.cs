using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Projects;

public sealed class ProjectTrackingTests
{
    [Fact]
    public void ProjectProgress_Create_ComDadosValidos_GeraRegistro()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var progress = ProjectProgress.Create(projectId, authorId, 42, "Migramos o primeiro bloco de servidores.");

        progress.ProjectId.Should().Be(projectId);
        progress.AuthorId.Should().Be(authorId);
        progress.Percent.Should().Be(42);
        progress.Comment.Should().Be("Migramos o primeiro bloco de servidores.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ProjectProgress_Create_ComPercentForaDoIntervalo_LancaArgumentException(int percent)
    {
        var act = () => ProjectProgress.Create(Guid.NewGuid(), Guid.NewGuid(), percent, "Atualização.");
        act.Should().Throw<ArgumentException>().WithMessage("*percentual*");
    }

    [Fact]
    public void ProjectImpediment_Create_Resolve_E_Reopen_AlternaEstado()
    {
        var projectId = Guid.NewGuid();
        var impediment = ProjectImpediment.Create(
            projectId,
            "Entregador de fibra atrasado",
            "O fornecedor não entregou a fibra até a data definida.",
            ProjectImpedimentCategory.Vendor,
            ProjectImpedimentSeverity.High,
            "Solicitar remanejamento de fornecedor e backup temporário.");

        impediment.Status.Should().Be(ProjectImpedimentStatus.Open);

        impediment.Resolve();
        impediment.Status.Should().Be(ProjectImpedimentStatus.Resolved);
        impediment.ResolvedAt.Should().NotBeNull();

        impediment.Reopen();
        impediment.Status.Should().Be(ProjectImpedimentStatus.Open);
        impediment.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void ProjectEvidence_Create_ValidateAndReject_AtualizaRevisao()
    {
        var projectId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var evidence = ProjectEvidence.Create(
            projectId,
            Guid.NewGuid(),
            ProjectEvidenceType.TestReport,
            "Relatório de failover executado",
            "failover.pdf",
            "application/pdf",
            2048);

        evidence.Status.Should().Be(ProjectEvidenceStatus.Pending);

        evidence.ValidateBy(reviewerId);
        evidence.Status.Should().Be(ProjectEvidenceStatus.Validated);

        var rejected = ProjectEvidence.Create(
            projectId,
            Guid.NewGuid(),
            ProjectEvidenceType.AcceptanceMinutes,
            "Ata de validação",
            "ata.pdf",
            "application/pdf",
            1024);

        rejected.RejectBy(reviewerId, "Faltaram assinaturas no documento.");
        rejected.Status.Should().Be(ProjectEvidenceStatus.Rejected);
        rejected.RejectionReason.Should().Be("Faltaram assinaturas no documento.");
    }

    [Fact]
    public void ProjectHistoryEntry_Create_TruncaDetalhesLongos()
    {
        var detail = new string('d', 600);
        var reason = new string('r', 600);

        var entry = ProjectHistoryEntry.Create(
            Guid.NewGuid(),
            ProjectHistoryType.ProgressReported,
            Guid.NewGuid(),
            detail,
            reason);

        entry.Detail.Should().HaveLength(500);
        entry.Reason.Should().HaveLength(500);
    }
}
