using FluentAssertions;
using InfraControl.Application.Knowledge.Categories.Commands;
using InfraControl.Application.Knowledge.Categories.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Knowledge;

/// <summary>
/// F10-T001: KnowledgeCategory ponta-a-ponta — CRUD, 404, 409
/// (nome duplicado) e 403 sem role de escrita.
/// </summary>
public sealed class KnowledgeCategoryIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task CategoryCrud_ShouldPersist()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var created = await mediator.Send(
            new CreateKnowledgeCategoryCommand("Redes", "Topologia e conectividade"));
        created.Name.Should().Be("Redes");

        await mediator.Send(
            new UpdateKnowledgeCategoryCommand(created.Id, "Segurança", null));

        var fetched = await mediator.Send(new GetKnowledgeCategoryQuery(created.Id));
        fetched.Name.Should().Be("Segurança");
        fetched.Description.Should().BeNull();

        var page = await mediator.Send(new SearchKnowledgeCategoriesQuery());
        page.Items.Should().ContainSingle(c => c.Id == created.Id);

        await mediator.Send(new DeleteKnowledgeCategoryCommand(created.Id));
        (await mediator.Send(new SearchKnowledgeCategoriesQuery())).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_DuplicateName_ShouldFailWith409PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        await mediator.Send(new CreateKnowledgeCategoryCommand("Redes", null));

        var action = () => mediator.Send(new CreateKnowledgeCategoryCommand("REDES", null));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma categoria com o nome 'REDES'.");
    }

    [Fact]
    public async Task Get_UnknownCategory_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new GetKnowledgeCategoryQuery(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Create_EmptyName_ShouldFailWith400PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var action = () => mediator.Send(new CreateKnowledgeCategoryCommand("  ", null));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome da categoria é obrigatório.*");
    }

    [Fact]
    public async Task Create_WithoutOperateRole_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(new CreateKnowledgeCategoryCommand("Sem permissão", null));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }
}
