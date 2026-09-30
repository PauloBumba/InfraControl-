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
/// F10-T002: KnowledgeArticle ponta-a-ponta — CRUD com categoria, 404
/// (artigo e categoria), 400 (título) e 403 sem role de escrita.
/// </summary>
public sealed class KnowledgeArticleIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task ArticleCrud_ShouldPersist()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var category = await mediator.Send(new CreateKnowledgeCategoryCommand("Redes", null));

        var created = await mediator.Send(new CreateKnowledgeArticleCommand(
            "Troca do switch central", "Failover passo a passo.", category.Id));
        created.Title.Should().Be("Troca do switch central");
        created.Status.Should().Be(KnowledgeArticleStatus.Draft);

        await mediator.Send(new UpdateKnowledgeArticleCommand(
            created.Id, "Troca do switch central (v2)", "Conteúdo novo.", null));

        var fetched = await mediator.Send(new GetKnowledgeArticleQuery(created.Id));
        fetched.Title.Should().Be("Troca do switch central (v2)");
        fetched.CategoryId.Should().BeNull();

        var page = await mediator.Send(new SearchKnowledgeArticlesQuery("switch"));
        page.Items.Should().ContainSingle(article => article.Id == created.Id);

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
            new CreateKnowledgeArticleCommand("Artigo", "Conteúdo.", unknown));

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
    public async Task Create_EmptyTitle_ShouldFailWith400PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var action = () => mediator.Send(
            new CreateKnowledgeArticleCommand("  ", "Conteúdo.", null));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O título do artigo é obrigatório.*");
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
