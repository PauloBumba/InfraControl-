using FluentAssertions;
using InfraControl.Application.Pdi;
using InfraControl.Application.Pdi.Commands;
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
/// Fase 3 do PDI (docs/30 §9-§11, §13, ADR-033): acompanhamento
/// bilateral sob a fase — progresso/comentários do colaborador,
/// check-in (relato do colaborador + resposta do gestor), impedimento
/// (tipo/descrição/impacto/status/resolvido em) e feedback (histórico
/// append-only, exclusão só Admin). Escopo: leitura dono/gestor/Admin,
/// relato dono/Admin, resposta gestor/Admin.
/// </summary>
public sealed class PdiAccompanimentTests
{
    [Fact]
    public async Task Progress_Create_ShouldPersistPercentAndComment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var handler = ProgressCreate(context, scene.Owner);

        var created = await handler.Handle(
            new CreatePdiProgressCommand(
                scene.PhaseId, Percent: 60,
                Comment: "Curso de liderança concluído"),
            CancellationToken.None);

        created.Percent.Should().Be(60);
        created.Comment.Should().Be("Curso de liderança concluído");
        created.AuthorName.Should().Be("Ana Dono");

        var badPercent = () => handler.Handle(
            new CreatePdiProgressCommand(scene.PhaseId, Percent: 101, Comment: "x"),
            CancellationToken.None);
        await badPercent.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*percentual*");

        var emptyComment = () => handler.Handle(
            new CreatePdiProgressCommand(scene.PhaseId, Comment: "   "),
            CancellationToken.None);
        await emptyComment.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*comentário*");

        (await context.PdiProgresses.ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Progress_Create_WhenUnknownPhaseOrForeignOperator_ShouldFail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var missing = ProgressCreate(context, scene.Owner);
        var unknownPhase = () => missing.Handle(
            new CreatePdiProgressCommand(Guid.NewGuid(), Comment: " órfão"),
            CancellationToken.None);
        await unknownPhase.Should().ThrowAsync<KeyNotFoundException>();

        var intruder = ProgressCreate(context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreign = () => intruder.Handle(
            new CreatePdiProgressCommand(scene.PhaseId, Comment: " invasor"),
            CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");

        (await context.PdiProgresses.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CheckIn_BilateralFlow_CollaboratorCreatesManagerResponds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        var checkIn = await CheckInCreate(context, scene.Owner).Handle(
            new CreatePdiCheckInCommand(
                scene.PhaseId, new DateOnly(2026, 10, 5),
                WhatDone: "Concluí o módulo de gestão",
                WhatLearned: "Delegação efetiva",
                Difficulty: "Falta de tempo",
                NeedSupport: "Sim, preciso cobertura nas sextas"),
            CancellationToken.None);

        checkIn.CreatedByName.Should().Be("Ana Dono");
        checkIn.RespondedAt.Should().BeNull("gestor ainda não respondeu");

        // Gestor responde → RespondedAt/RespondedById preenchidos.
        await CheckInUpdate(context, scene.Manager).Handle(
            new UpdatePdiCheckInCommand(
                checkIn.Id,
                WhatDone: "texto que o gestor NÃO pode sobrescrever",
                ManagerFeedback: "Evolução positiva",
                NextAction: "Delegar uma atividade pequena"),
            CancellationToken.None);

        var afterResponse = await context.PdiCheckIns.SingleAsync();
        afterResponse.RespondedAt.Should().NotBeNull();
        afterResponse.RespondedById.Should().Be(scene.Manager.Id);
        afterResponse.ManagerFeedback.Should().Be("Evolução positiva");
        afterResponse.NextAction.Should().Be("Delegar uma atividade pequena");
        afterResponse.WhatDone.Should().Be(
            "Concluí o módulo de gestão",
            "o gestor não edita o relato do colaborador");

        // Colaborador edita o próprio relato (mantém a resposta intacta).
        await CheckInUpdate(context, scene.Owner).Handle(
            new UpdatePdiCheckInCommand(
                checkIn.Id, WhatDone: "Concluí o módulo e apresentei os aprendizados"),
            CancellationToken.None);

        var afterEdit = await context.PdiCheckIns.SingleAsync();
        afterEdit.WhatDone.Should().Be("Concluí o módulo e apresentei os aprendizados");
        afterEdit.ManagerFeedback.Should().Be("Evolução positiva");
        afterEdit.RespondedById.Should().Be(scene.Manager.Id);

        // Admin edita o relato: PUT por lado não re-carimba a resposta
        // (RespondedById permanece do gestor que respondeu).
        var admin = new FakeCurrentUser(Guid.NewGuid(), "Admin");
        await CheckInUpdate(context, admin).Handle(
            new UpdatePdiCheckInCommand(
                checkIn.Id, WhatDone: "Relato revisado pela administração"),
            CancellationToken.None);

        var afterAdminEdit = await context.PdiCheckIns.SingleAsync();
        afterAdminEdit.WhatDone.Should().Be("Relato revisado pela administração");
        afterAdminEdit.RespondedById.Should().Be(
            scene.Manager.Id,
            "PUT só de relato não deve re-carimbar RespondedById");
    }

    [Fact]
    public async Task CheckIn_Update_WhenStranger_Should403_AndOwnerlessPlan_IgnoresResponse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context, withManager: false);
        var checkIn = await CheckInCreate(context, scene.Owner).Handle(
            new CreatePdiCheckInCommand(
                scene.PhaseId, new DateOnly(2026, 10, 5), WhatDone: "Relato"),
            CancellationToken.None);

        var intruder = CheckInUpdate(context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreign = () => intruder.Handle(
            new UpdatePdiCheckInCommand(checkIn.Id, WhatDone: "capturado"),
            CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*check-in*");

        // Sem gestor no plano: o dono edita o relato, mas a resposta é ignorada.
        await CheckInUpdate(context, scene.Owner).Handle(
            new UpdatePdiCheckInCommand(
                checkIn.Id, WhatDone: "Relato atualizado",
                ManagerFeedback: "resposta que ninguém pode dar"),
            CancellationToken.None);

        var stored = await context.PdiCheckIns.SingleAsync();
        stored.WhatDone.Should().Be("Relato atualizado");
        stored.ManagerFeedback.Should().BeNull("plano sem gestor definido");
        stored.RespondedAt.Should().BeNull();

        var emptyWhatDone = () => CheckInCreate(context, scene.Owner).Handle(
            new CreatePdiCheckInCommand(
                scene.PhaseId, new DateOnly(2026, 10, 6), WhatDone: "  "),
            CancellationToken.None);
        await emptyWhatDone.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*realizado*");
    }

    [Fact]
    public async Task Impediment_CreateResolveReopen_ShouldValidatePtBr()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var handler = ImpedimentCreate(context, scene.Owner);

        var impediment = await handler.Handle(
            new CreatePdiImpedimentCommand(
                scene.PhaseId, Type: "Recurso indisponível",
                Description: "Treinamento não disponibilizado",
                Impact: "Atraso na fase",
                OccurredAt: new DateOnly(2026, 10, 2),
                ResponsibleId: scene.Manager.Id),
            CancellationToken.None);

        impediment.Status.Should().Be("Open");
        impediment.ResponsibleName.Should().Be("Carla Gestor");

        var emptyType = () => handler.Handle(
            new CreatePdiImpedimentCommand(
                scene.PhaseId, Type: " ", Description: "x",
                OccurredAt: new DateOnly(2026, 10, 2)),
            CancellationToken.None);
        await emptyType.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tipo*");

        var unknownResponsible = () => handler.Handle(
            new CreatePdiImpedimentCommand(
                scene.PhaseId, Type: "Pessoal", Description: "x",
                OccurredAt: new DateOnly(2026, 10, 2),
                ResponsibleId: Guid.NewGuid()),
            CancellationToken.None);
        await unknownResponsible.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Responsável*");

        // Resolver preenche ResolvedAt; reabrir limpa.
        await ImpedimentUpdate(context, scene.Owner).Handle(
            new UpdatePdiImpedimentCommand(
                impediment.Id, Type: "Recurso indisponível",
                Description: "Treinamento não disponibilizado",
                Impact: "Atraso na fase",
                OccurredAt: new DateOnly(2026, 10, 2),
                ResponsibleId: scene.Manager.Id,
                Status: "resolved"),
            CancellationToken.None);

        var resolved = await context.PdiImpediments.SingleAsync();
        resolved.Status.Should().Be(PdiImpedimentStatus.Resolved);
        resolved.ResolvedAt.Should().NotBeNull();

        var badStatus = () => ImpedimentUpdate(context, scene.Owner).Handle(
            new UpdatePdiImpedimentCommand(
                impediment.Id, Type: "Recurso indisponível",
                Description: "Treinamento não disponibilizado",
                OccurredAt: new DateOnly(2026, 10, 2), Status: "Qualquer"),
            CancellationToken.None);
        await badStatus.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Status*");

        await ImpedimentUpdate(context, scene.Owner).Handle(
            new UpdatePdiImpedimentCommand(
                impediment.Id, Type: "Recurso indisponível",
                Description: "Treinamento não disponibilizado",
                OccurredAt: new DateOnly(2026, 10, 2), Status: "open"),
            CancellationToken.None);

        var reopened = await context.PdiImpediments.SingleAsync();
        reopened.Status.Should().Be(PdiImpedimentStatus.Open);
        reopened.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Impediment_Update_ByManagerAllowed_StrangerForbidden()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var impediment = await ImpedimentCreate(context, scene.Owner).Handle(
            new CreatePdiImpedimentCommand(
                scene.PhaseId, Type: "Pessoal", Description: "Conflito de agenda",
                OccurredAt: new DateOnly(2026, 10, 3)),
            CancellationToken.None);

        await ImpedimentUpdate(context, scene.Manager).Handle(
            new UpdatePdiImpedimentCommand(
                impediment.Id, Type: "Pessoal", Description: "Conflito de agenda",
                OccurredAt: new DateOnly(2026, 10, 3),
                ActionTaken: "Reunião remarcada", Status: "open"),
            CancellationToken.None);

        (await context.PdiImpediments.SingleAsync()).ActionTaken
            .Should().Be("Reunião remarcada");

        var intruder = ImpedimentUpdate(context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreign = () => intruder.Handle(
            new UpdatePdiImpedimentCommand(
                impediment.Id, Type: "Pessoal", Description: "capturado",
                OccurredAt: new DateOnly(2026, 10, 3)),
            CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");
    }

    [Fact]
    public async Task Feedback_Create_ShouldValidateRecipientPersistAndResolveNames()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var handler = FeedbackCreate(context, scene.Manager);

        var created = await handler.Handle(
            new CreatePdiFeedbackCommand(
                scene.PhaseId, RecipientId: scene.Owner.Id,
                Type: "Evolução",
                Content: "Você apresentou evolução na delegação.",
                FeedbackDate: new DateOnly(2026, 10, 15)),
            CancellationToken.None);

        created.AuthorName.Should().Be("Carla Gestor");
        created.RecipientName.Should().Be("Ana Dono");

        var strangerRecipient = () => handler.Handle(
            new CreatePdiFeedbackCommand(
                scene.PhaseId, RecipientId: Guid.NewGuid(),
                Type: "Evolução", Content: "x",
                FeedbackDate: new DateOnly(2026, 10, 15)),
            CancellationToken.None);
        await strangerRecipient.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*destinatário*");

        var emptyContent = () => handler.Handle(
            new CreatePdiFeedbackCommand(
                scene.PhaseId, RecipientId: scene.Owner.Id,
                Type: "Evolução", Content: "   ",
                FeedbackDate: new DateOnly(2026, 10, 15)),
            CancellationToken.None);
        await emptyContent.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*conteúdo*");

        (await context.PdiFeedbacks.ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Feedback_Delete_OnlyAdmin_ShouldPass()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);
        var feedback = await FeedbackCreate(context, scene.Owner).Handle(
            new CreatePdiFeedbackCommand(
                scene.PhaseId, RecipientId: scene.Owner.Id,
                Type: "Geral", Content: "Registro de feedback.",
                FeedbackDate: new DateOnly(2026, 10, 15)),
            CancellationToken.None);

        var nonAdmin = FeedbackDelete(context, scene.Owner);
        var forbidden = () => nonAdmin.Handle(
            new DeletePdiFeedbackCommand(feedback.Id), CancellationToken.None);
        await forbidden.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*administradores*");

        await FeedbackDelete(context, new FakeCurrentUser(Guid.NewGuid(), "Admin"))
            .Handle(new DeletePdiFeedbackCommand(feedback.Id), CancellationToken.None);
        (await context.PdiFeedbacks.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AccompanimentQuery_ShouldGroupAllFourResolveNamesAndScope()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();

        var scene = await SeedSceneAsync(context);

        await ProgressCreate(context, scene.Owner).Handle(
            new CreatePdiProgressCommand(scene.PhaseId, Percent: 30, Comment: "Início"),
            CancellationToken.None);
        var checkIn = await CheckInCreate(context, scene.Owner).Handle(
            new CreatePdiCheckInCommand(
                scene.PhaseId, new DateOnly(2026, 10, 5), WhatDone: "Relato"),
            CancellationToken.None);
        await CheckInUpdate(context, scene.Manager).Handle(
            new UpdatePdiCheckInCommand(
                checkIn.Id, ManagerFeedback: "Bom trabalho"),
            CancellationToken.None);
        await ImpedimentCreate(context, scene.Owner).Handle(
            new CreatePdiImpedimentCommand(
                scene.PhaseId, Type: "Pessoal", Description: "Agenda",
                OccurredAt: new DateOnly(2026, 10, 4)),
            CancellationToken.None);
        await FeedbackCreate(context, scene.Manager).Handle(
            new CreatePdiFeedbackCommand(
                scene.PhaseId, RecipientId: scene.Owner.Id,
                Type: "Evolução", Content: "Segue elogio.",
                FeedbackDate: new DateOnly(2026, 10, 6)),
            CancellationToken.None);

        // Admin lê qualquer plano; gestor também (bilateral §9).
        foreach (var reader in new[]
                 {
                     new FakeCurrentUser(Guid.NewGuid(), "Admin"),
                     scene.Manager, scene.Owner
                 })
        {
            var dto = await AccompanimentQuery(context, reader).Handle(
                new GetPdiAccompanimentQuery(scene.Owner.Id), CancellationToken.None);

            dto.UserId.Should().Be(scene.Owner.Id);
            dto.Progresses.Should().ContainSingle()
                .Which.AuthorName.Should().Be("Ana Dono");
            dto.CheckIns.Should().ContainSingle().Which.Should().Match(
                (PdiCheckInDto item) =>
                    item.ManagerFeedback == "Bom trabalho"
                    && item.RespondedByName == "Carla Gestor");
            dto.Impediments.Should().ContainSingle()
                .Which.Type.Should().Be("Pessoal");
            dto.Feedbacks.Should().ContainSingle().Which.Should().Match(
                (PdiFeedbackDto item) =>
                    item.AuthorName == "Carla Gestor"
                    && item.RecipientName == "Ana Dono");
        }

        var stranger = AccompanimentQuery(
            context, new FakeCurrentUser(Guid.NewGuid(), "Operator"));
        var foreign = () => stranger.Handle(
            new GetPdiAccompanimentQuery(scene.Owner.Id), CancellationToken.None);
        await foreign.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*próprio*");

        var noPlanUser = await SeedUserAsync(context, "sem-plano@exemplo.test", "Sem");
        var missing = AccompanimentQuery(
            context, new FakeCurrentUser(noPlanUser.Id, "Operator"));
        var absent = () => missing.Handle(
            new GetPdiAccompanimentQuery(noPlanUser.Id), CancellationToken.None);
        await absent.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---- Fixtures -------------------------------------------------

    private sealed record SceneActors(
        FakeCurrentUser Owner,
        FakeCurrentUser Manager,
        Guid PhaseId);

    private static async Task<SceneActors> SeedSceneAsync(
        AppDbContext context, bool withManager = true)
    {
        var owner = await SeedUserAsync(context, "dono@exemplo.test", "Ana Dono");
        var manager = await SeedUserAsync(context, "gestor@exemplo.test", "Carla Gestor");
        await SeedPlanAsync(context, owner.Id, withManager ? manager.Id : null);

        var ownerActor = new FakeCurrentUser(owner.Id, "Operator");
        var managerActor = new FakeCurrentUser(manager.Id, "Operator");

        var objective = await ObjectiveCreate(context, ownerActor).Handle(
            new CreatePdiObjectiveCommand(owner.Id, "Assumir liderança"),
            CancellationToken.None);
        var strategy = await StrategyCreate(context, ownerActor).Handle(
            new CreatePdiStrategyCommand(objective.Id, "Formação em gestão"),
            CancellationToken.None);
        var phase = await PhaseCreate(context, ownerActor).Handle(
            new CreatePdiPhaseCommand(strategy.Id, "Primeiros passos"),
            CancellationToken.None);

        return new SceneActors(ownerActor, managerActor, phase.Id);
    }

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext context, string email, string displayName)
    {
        var user = User.Create(email, displayName);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<IndividualDevelopmentPlan> SeedPlanAsync(
        AppDbContext context, Guid ownerId, Guid? managerId)
    {
        var plan = IndividualDevelopmentPlan.Create(
            ownerId, "Plano do ciclo", new DateOnly(2027, 6, 30),
            PdiStatus.Active, managerId, null, null,
            new PdiChangeActor(ownerId, $"dono-{ownerId}", "dono@exemplo.test"));
        context.IndividualDevelopmentPlans.Add(plan);
        await context.SaveChangesAsync();
        return plan;
    }

    private static CreatePdiProgressCommandHandler ProgressCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiProgressCommandHandler>.Instance);

    private static CreatePdiCheckInCommandHandler CheckInCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiCheckInCommandHandler>.Instance);

    private static UpdatePdiCheckInCommandHandler CheckInUpdate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<UpdatePdiCheckInCommandHandler>.Instance);

    private static CreatePdiImpedimentCommandHandler ImpedimentCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiImpedimentCommandHandler>.Instance);

    private static UpdatePdiImpedimentCommandHandler ImpedimentUpdate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<UpdatePdiImpedimentCommandHandler>.Instance);

    private static CreatePdiFeedbackCommandHandler FeedbackCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiFeedbackCommandHandler>.Instance);

    private static DeletePdiFeedbackCommandHandler FeedbackDelete(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), user,
            NullLogger<DeletePdiFeedbackCommandHandler>.Instance);

    private static GetPdiAccompanimentQueryHandler AccompanimentQuery(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user);

    private static CreatePdiObjectiveCommandHandler ObjectiveCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiObjectiveCommandHandler>.Instance);

    private static CreatePdiStrategyCommandHandler StrategyCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiStrategyCommandHandler>.Instance);

    private static CreatePdiPhaseCommandHandler PhaseCreate(
        AppDbContext context, FakeCurrentUser user)
        => new(new PdiStructureRepository(context), new PdiRepository(context), user,
            NullLogger<CreatePdiPhaseCommandHandler>.Instance);

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
