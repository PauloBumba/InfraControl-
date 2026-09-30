using FluentAssertions;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.Application.Knowledge.Categories.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Knowledge;

/// <summary>
/// F10-T002: KnowledgeArticle ponta-a-ponta — CRUD com FK de categoria,
/// 404, busca por status e categoria com artigos (409).
/// </summary>
public sealed class KnowledgeArticleIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedCategoryAsync(IMediator mediator, string name)
        => (await mediator.Send(new CreateKnowledgeCategoryCommand(name, null))).Id;

    [Fact]
    public async Task ArticleCrud_ShouldPersistAsDraft()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var categoryId = await SeedCategoryAsync(mediator, "Redes");

        var created = await mediator.Send(new CreateKnowledgeArticleCommand(
            "Como trocar o switch", "Passo a passo.", categoryId));
        created.Status.Should().Be(KnowledgeArticleStatus.Draft);

        await mediator.Send(new UpdateKnowledgeArticleCommand(
            created.Id, "Como trocar o switch v2", "Passo a passo v2.", categoryId));

        var fetched = await mediator.Send(new GetKnowledgeArticleQuery(created.Id));
        fetched.Title.Should().Be("Como trocar o switch v2");

        var page = await mediator.Send(new SearchKnowledgeArticlesQuery());
        page.Items.Should().ContainSingle(a => a.Id == created.Id);

        await mediator.Send(new DeleteKnowledgeArticleCommand(created.Id));
        (await mediator.Send(new SearchKnowledgeArticlesQuery())).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_UnknownCategory_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(
            new CreateKnowledgeArticleCommand("Órfão", "Conteúdo.", unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Categoria '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Get_UnknownArticle_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new GetKnowledgeArticleQuery(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task DeleteCategory_WithArticles_ShouldFailWith409PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var categoryId = await SeedCategoryAsync(mediator, "Backup");
        await mediator.Send(new CreateKnowledgeArticleCommand(
            "Rotina de backup", "Conteúdo.", categoryId));

        var action = () => mediator.Send(new DeleteKnowledgeCategoryCommand(categoryId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir a categoria: existem artigos vinculados.");
    }

    [Fact]
    public async Task Create_WithoutOperateRole_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new CreateKnowledgeArticleCommand("Sem permissão", "Conteúdo.", null));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }
}
