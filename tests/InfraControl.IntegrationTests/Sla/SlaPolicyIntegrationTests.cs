using FluentAssertions;
using InfraControl.Application.Sla.Policies.Commands;
using InfraControl.Application.Sla.Policies.Queries;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Sla;

/// <summary>
/// F8-T002: SlaPolicy ponta-a-ponta — CRUD, FK de serviço (404),
/// exclusão de serviço com policies (409) e 403 sem permissão.
/// </summary>
public sealed class SlaPolicyIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedServiceAsync(IMediator mediator, string name)
        => (await mediator.Send(new CreateServiceCommand(name))).Id;

    [Fact]
    public async Task SlaPolicyCrud_ShouldPersistWithServiceBinding()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var serviceId = await SeedServiceAsync(mediator, "ERP Corporativo");

        var created = await mediator.Send(
            new CreateSlaPolicyCommand("Padrão ERP", 30, 480, serviceId));
        created.ServiceId.Should().Be(serviceId);

        await mediator.Send(
            new UpdateSlaPolicyCommand(created.Id, "Padrão ERP v2", 45, 720, serviceId));

        var fetched = await mediator.Send(new GetSlaPolicyQuery(created.Id));
        fetched.Name.Should().Be("Padrão ERP v2");
        fetched.ResponseMinutes.Should().Be(45);
        fetched.ResolutionMinutes.Should().Be(720);

        var page = await mediator.Send(new SearchSlaPoliciesQuery(ServiceId: serviceId));
        page.Items.Should().ContainSingle(policy => policy.Id == created.Id);

        await mediator.Send(new DeleteSlaPolicyCommand(created.Id));
        (await mediator.Send(new SearchSlaPoliciesQuery())).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UnknownService_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(
            new CreateSlaPolicyCommand("Órfã", 30, 480, unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Serviço '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task DeleteService_WithPolicies_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var serviceId = await SeedServiceAsync(mediator, "Intranet");
        await mediator.Send(
            new CreateSlaPolicyCommand("Da Intranet", 15, 240, serviceId));

        var action = () => mediator.Send(new DeleteServiceCommand(serviceId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o serviço: existem políticas de SLA vinculadas.");
    }

    [Fact]
    public async Task Create_WithoutSlaManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(
            new CreateSlaPolicyCommand("Sem permissão", 30, 480));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: SlaManage");
    }

    [Fact]
    public async Task Create_ZeroResponseMinutes_ShouldThrowArgumentExceptionPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var action = () => mediator.Send(
            new CreateSlaPolicyCommand("Sem prazo", 0, 480));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O prazo de resposta deve ser maior que zero.*");
    }
}
