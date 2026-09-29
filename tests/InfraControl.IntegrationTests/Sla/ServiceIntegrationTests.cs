using FluentAssertions;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.Application.Sla.Services.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Sla;

/// <summary>
/// F8-T001: Service ponta-a-ponta — CRUD no pipeline real com policies
/// `SlaRead`/`SlaManage`, 403 sem permissão e 404 pt-BR.
/// </summary>
public sealed class ServiceIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task ServiceCrud_ShouldPersistAndReturnDto()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var created = await mediator.Send(
            new CreateServiceCommand("ERP Corporativo", "Núcleo de negócio"));
        created.Id.Should().NotBe(Guid.Empty);

        await mediator.Send(
            new UpdateServiceCommand(created.Id, "ERP Corporativo v2", "Atualizado"));

        var fetched = await mediator.Send(new GetServiceQuery(created.Id));
        fetched.Name.Should().Be("ERP Corporativo v2");
        fetched.Description.Should().Be("Atualizado");

        var page = await mediator.Send(new SearchServicesQuery(Term: "erp"));
        page.Items.Should().ContainSingle(service => service.Id == created.Id);

        await mediator.Send(new DeleteServiceCommand(created.Id));
        var afterDelete = await mediator.Send(new SearchServicesQuery());
        afterDelete.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_DuplicateName_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        await mediator.Send(new CreateServiceCommand("Intranet"));

        var action = () => mediator.Send(new CreateServiceCommand("intranet"));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um serviço com este nome.");
    }

    [Fact]
    public async Task Create_WithoutSlaManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(new CreateServiceCommand("Sem Permissão"));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }

    [Fact]
    public async Task Search_WithoutSlaRead_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(new SearchServicesQuery());

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator, Viewer");
    }

    [Fact]
    public async Task Get_UnknownService_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new GetServiceQuery(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Serviço '{unknown}' não encontrado.");
    }
}
