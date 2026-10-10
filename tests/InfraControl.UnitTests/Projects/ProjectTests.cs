using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Projects;

public sealed class ProjectTests
{
    private readonly Guid _leaderId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    [Fact]
    public void Create_ComDadosValidos_CriaProjetoComStatusDraftEEvento()
    {
        var project = Project.Create(
            code: "PRJ-001",
            title: "Migração de Datacenter",
            description: "Migração de servidores físicos para cluster Proxmox",
            leaderId: _leaderId,
            priority: ProjectPriority.High,
            justification: "Fim de vida útil dos servidores legados",
            managerId: _managerId,
            plannedStartDate: new DateOnly(2026, 11, 1),
            plannedEndDate: new DateOnly(2026, 12, 1));

        project.Id.Should().NotBeEmpty();
        project.Code.Should().Be("PRJ-001");
        project.Title.Should().Be("Migração de Datacenter");
        project.Description.Should().Be("Migração de servidores físicos para cluster Proxmox");
        project.LeaderId.Should().Be(_leaderId);
        project.Priority.Should().Be(ProjectPriority.High);
        project.Status.Should().Be(ProjectStatus.Draft);
        project.Justification.Should().Be("Fim de vida útil dos servidores legados");
        project.ManagerId.Should().Be(_managerId);
        project.PlannedStartDate.Should().Be(new DateOnly(2026, 11, 1));
        project.PlannedEndDate.Should().Be(new DateOnly(2026, 12, 1));

        project.DomainEvents.Should().ContainSingle(e => e is ProjectCreatedDomainEvent);
        var createdEvent = project.DomainEvents.OfType<ProjectCreatedDomainEvent>().Single();
        createdEvent.ProjectId.Should().Be(project.Id);
        createdEvent.Code.Should().Be("PRJ-001");
        createdEvent.Title.Should().Be("Migração de Datacenter");
        createdEvent.LeaderId.Should().Be(_leaderId);
    }

    [Theory]
    [InlineData("", "Título", "Descrição")]
    [InlineData("PRJ-01", "", "Descrição")]
    [InlineData("PRJ-01", "Título", "")]
    public void Create_ComCamposObrigatoriosVazios_LancaArgumentException(string code, string title, string description)
    {
        var act = () => Project.Create(code, title, description, _leaderId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ComLeaderIdVazio_LancaArgumentException()
    {
        var act = () => Project.Create("PRJ-001", "Título", "Descrição", Guid.Empty);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*líder técnico*");
    }

    [Fact]
    public void Create_ComDataFinalAnteriorADataInicial_LancaArgumentException()
    {
        var act = () => Project.Create(
            "PRJ-001", "Título", "Descrição", _leaderId,
            plannedStartDate: new DateOnly(2026, 12, 1),
            plannedEndDate: new DateOnly(2026, 11, 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*não pode ser anterior*");
    }

    [Fact]
    public void UpdateDetails_AtualizaPropriedadesEEmiteEvento()
    {
        var project = Project.Create("PRJ-001", "Título Original", "Desc Original", _leaderId);
        project.ClearDomainEvents();

        var newLeader = Guid.NewGuid();
        var newManager = Guid.NewGuid();

        project.UpdateDetails(
            "Título Atualizado",
            "Nova Descrição",
            "Nova Justificativa",
            ProjectPriority.Critical,
            newLeader,
            newManager);

        project.Title.Should().Be("Título Atualizado");
        project.Description.Should().Be("Nova Descrição");
        project.Justification.Should().Be("Nova Justificativa");
        project.Priority.Should().Be(ProjectPriority.Critical);
        project.LeaderId.Should().Be(newLeader);
        project.ManagerId.Should().Be(newManager);

        project.DomainEvents.Should().ContainSingle(e => e is ProjectUpdatedDomainEvent);
    }

    [Fact]
    public void SetPlanningDates_E_SetActualDates_ValidaIntervalos()
    {
        var project = Project.Create("PRJ-001", "Título", "Descrição", _leaderId);

        project.SetPlanningDates(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        project.PlannedStartDate.Should().Be(new DateOnly(2026, 10, 1));
        project.PlannedEndDate.Should().Be(new DateOnly(2026, 10, 31));

        var actPlanning = () => project.SetPlanningDates(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1));
        actPlanning.Should().Throw<ArgumentException>();

        project.SetActualDates(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 25));
        project.ActualStartDate.Should().Be(new DateOnly(2026, 10, 5));
        project.ActualEndDate.Should().Be(new DateOnly(2026, 10, 25));

        var actActual = () => project.SetActualDates(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 5));
        actActual.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CicloDeVida_FluxoCompletoAteAprovacaoAdmin()
    {
        var project = Project.Create("PRJ-001", "Upgrade Wi-Fi 6", "Substituição de APs", _leaderId);
        project.Status.Should().Be(ProjectStatus.Draft);

        // Draft -> Planned
        project.TransitionTo(ProjectStatus.Planned);
        project.Status.Should().Be(ProjectStatus.Planned);

        // Planned -> InProgress
        project.TransitionTo(ProjectStatus.InProgress);
        project.Status.Should().Be(ProjectStatus.InProgress);
        project.ActualStartDate.Should().NotBeNull();

        // InProgress -> PendingFinalApproval
        project.SubmitForApproval();
        project.Status.Should().Be(ProjectStatus.PendingFinalApproval);

        // Homologação pelo Administrador
        var approvedAt = DateTimeOffset.UtcNow;
        project.Approve(_adminId, "Projeto homologado e aprovado com sucesso.", approvedAt);

        project.Status.Should().Be(ProjectStatus.Completed);
        project.AdminApproverId.Should().Be(_adminId);
        project.ApprovalNotes.Should().Be("Projeto homologado e aprovado com sucesso.");
        project.ApprovedAt.Should().Be(approvedAt);
        project.ActualEndDate.Should().Be(DateOnly.FromDateTime(approvedAt.UtcDateTime));

        project.DomainEvents.Should().Contain(e => e is ProjectApprovedDomainEvent);
    }

    [Fact]
    public void TransicaoInvalida_LancaInvalidOperationException()
    {
        var project = Project.Create("PRJ-001", "Título", "Descrição", _leaderId);
        // Não pode ir de Draft direto para Completed
        var act = () => project.TransitionTo(ProjectStatus.Completed);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Transição inválida*");
    }

    [Fact]
    public void PutOnHold_E_Resume_AlternaCorretamente()
    {
        var project = Project.Create("PRJ-001", "Título", "Descrição", _leaderId);
        project.TransitionTo(ProjectStatus.Planned);
        project.TransitionTo(ProjectStatus.InProgress);

        project.PutOnHold("Aguardando entrega de cabos de rede pelo fornecedor");
        project.Status.Should().Be(ProjectStatus.OnHold);

        project.Resume();
        project.Status.Should().Be(ProjectStatus.InProgress);
    }

    [Fact]
    public void Cancel_ComMotivo_CancelaProjeto()
    {
        var project = Project.Create("PRJ-001", "Título", "Descrição", _leaderId);
        project.Cancel("Mudança de prioridade estratégica da diretoria.");

        project.Status.Should().Be(ProjectStatus.Cancelled);

        // Projeto cancelado não pode ser alterado
        var act = () => project.UpdateDetails("Novo", "Novo", null, ProjectPriority.Low, _leaderId, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Membros_AdicionarEremoverMembros()
    {
        var project = Project.Create("PRJ-001", "Título", "Descrição", _leaderId);
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        project.AddMember(user1, ProjectMemberRole.Member);
        project.AddMember(user2, ProjectMemberRole.Reviewer);

        project.Members.Should().HaveCount(2);
        project.Members.Should().Contain(m => m.UserId == user1 && m.Role == ProjectMemberRole.Member);
        project.Members.Should().Contain(m => m.UserId == user2 && m.Role == ProjectMemberRole.Reviewer);

        // Atualizar papel de membro existente
        project.AddMember(user1, ProjectMemberRole.Leader);
        project.Members.Should().HaveCount(2);
        project.Members.Single(m => m.UserId == user1).Role.Should().Be(ProjectMemberRole.Leader);

        // Remover membro
        project.RemoveMember(user2);
        project.Members.Should().HaveCount(1);
        project.Members.Should().NotContain(m => m.UserId == user2);
    }
}
