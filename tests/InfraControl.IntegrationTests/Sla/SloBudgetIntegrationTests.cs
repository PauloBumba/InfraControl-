using FluentAssertions;
using InfraControl.Application.Sla.Slos.Budgets.Commands;
using InfraControl.Application.Sla.Slos.Budgets.Queries;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Sla;

/// <summary>
/// F8-T007: orçamento de SLO ponta-a-ponta — registro, busca cronológica,
/// 400 (percentual/período), 404, 409 (delete do SLO) e 403.
/// </summary>
public sealed class SloBudgetIntegrationTests : BaseIntegrationTest
{
    private static readonly DateTime Start =
        new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<Guid> SeedSloAsync(IMediator mediator, string service)
    {
        var serviceId = (await mediator.Send(new CreateServiceCommand(service))).Id;
        return (await mediator.Send(
            new CreateSloCommand($"Disponibilidade {service}", 99.95m, 30, serviceId))).Id;
    }

    [Fact]
    public async Task RecordAndSearchBudgets_ShouldPersistChronologically()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var sloId = await SeedSloAsync(mediator, "ERPX");

        var first = await mediator.Send(new RecordSloBudgetCommand(
            sloId, Start.AddDays(30), null, 42.5m));
        var second = await mediator.Send(new RecordSloBudgetCommand(
            sloId, Start, Start.AddDays(30), 20m));

        var page = await mediator.Send(new SearchSloBudgetsQuery(sloId));
        page.TotalCount.Should().Be(2);
        page.Items.Select(b => b.PeriodStart).Should().BeInDescendingOrder();

        var fetched = await mediator.Send(new GetSloBudgetQuery(first.Id));
        fetched.ConsumedPercent.Should().Be(42.5m);

        await mediator.Send(new DeleteSloBudgetCommand(second.Id));
        (await mediator.Send(new SearchSloBudgetsQuery(sloId))).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Record_ConsumedOver100_ShouldThrowArgumentExceptionPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var sloId = await SeedSloAsync(mediator, "ERPY");

        var action = () => mediator.Send(new RecordSloBudgetCommand(
            sloId, Start, null, 100.5m));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O percentual consumido deve estar entre 0 e 100.*");
    }

    [Fact]
    public async Task Record_UnknownSlo_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new RecordSloBudgetCommand(
            unknown, Start, null, 10m));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"SLO '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task DeleteSlo_WithBudgets_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var sloId = await SeedSloAsync(mediator, "ERPZ");
        await mediator.Send(new RecordSloBudgetCommand(sloId, Start, null, 10m));

        var action = () => mediator.Send(new DeleteSloCommand(sloId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o SLO: existem orçamentos vinculados.");
    }

    [Fact]
    public async Task Record_WithoutSlaManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(new RecordSloBudgetCommand(
            Guid.NewGuid(), Start, null, 10m));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: SlaManage");
    }
}
