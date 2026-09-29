using FluentAssertions;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.Application.Sla.Slos.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Sla;

/// <summary>
/// F8-T005: Slo ponta-a-ponta — CRUD, FK de serviço (404), 400 (alvo),
/// exclusão de serviço com SLOs (409) e 403.
/// </summary>
public sealed class SloIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedServiceAsync(IMediator mediator, string name)
        => (await mediator.Send(new CreateServiceCommand(name))).Id;

    [Fact]
    public async Task SloCrud_ShouldPersistWithServiceBinding()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var serviceId = await SeedServiceAsync(mediator, "ERP Corporativo");

        var created = await mediator.Send(
            new CreateSloCommand("Disponibilidade do ERP", 99.95m, 30, serviceId));
        created.TargetPercent.Should().Be(99.95m);

        await mediator.Send(
            new UpdateSloCommand(created.Id, "Disponibilidade v2", 99.99m, 90, serviceId));

        var fetched = await mediator.Send(new GetSloQuery(created.Id));
        fetched.Name.Should().Be("Disponibilidade v2");
        fetched.WindowDays.Should().Be(90);

        var page = await mediator.Send(new SearchSlosQuery(ServiceId: serviceId));
        page.Items.Should().ContainSingle(slo => slo.Id == created.Id);

        await mediator.Send(new DeleteSloCommand(created.Id));
        (await mediator.Send(new SearchSlosQuery())).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UnknownService_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(
            new CreateSloCommand("Órfão", 99.9m, 30, unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Serviço '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task DeleteService_WithSlos_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var serviceId = await SeedServiceAsync(mediator, "Intranet");
        await mediator.Send(
            new CreateSloCommand("Disponibilidade Intranet", 99.9m, 30, serviceId));

        var action = () => mediator.Send(new DeleteServiceCommand(serviceId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o serviço: existem SLOs vinculados.");
    }

    [Fact]
    public async Task Create_TargetPercentOver100_ShouldThrowArgumentExceptionPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var serviceId = await SeedServiceAsync(mediator, "ERPX");

        var action = () => mediator.Send(
            new CreateSloCommand("Impossível", 100.5m, 30, serviceId));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O alvo do SLO deve estar entre 0 (exclusivo) e 100.*");
    }

    [Fact]
    public async Task Create_WithoutSlaManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new CreateSloCommand("Sem permissão", 99.9m, 30, Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }
}
