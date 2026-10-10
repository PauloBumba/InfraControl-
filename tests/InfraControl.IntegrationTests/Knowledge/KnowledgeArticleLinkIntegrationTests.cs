using FluentAssertions;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.IntegrationTests.Common;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Knowledge;

/// <summary>
/// F10-T004: vínculo Artigo ↔ Device ponta-a-ponta no pipeline real —
/// link → GET com id → unlink, 404/409 pt-BR e escrita só com Operate.
/// </summary>
public sealed class KnowledgeArticleLinkIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task LinkDevice_Lifecycle_ShouldRoundTripViaQueries()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.75");
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var article = await mediator.Send(
            new CreateKnowledgeArticleCommand("Artigo vinculado", "Conteudo.", null));

        await mediator.Send(new LinkKnowledgeArticleDeviceCommand(article.Id, device.Id));

        var fetched = await mediator.Send(new GetKnowledgeArticleQuery(article.Id));
        fetched.DeviceIds.Should().Equal(device.Id);

        await mediator.Send(new UnlinkKnowledgeArticleDeviceCommand(article.Id, device.Id));

        var after = await mediator.Send(new GetKnowledgeArticleQuery(article.Id));
        after.DeviceIds.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkDevice_UnknownTarget_ShouldThrowNotFoundPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var article = await mediator.Send(
            new CreateKnowledgeArticleCommand("Artigo solitario", "Conteudo.", null));
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(
            new LinkKnowledgeArticleDeviceCommand(article.Id, unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task LinkDevice_Duplicate_ShouldThrowConflict409PtBr()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await SeedDeviceAsync(dbContext, ip: "192.0.2.76");
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var article = await mediator.Send(
            new CreateKnowledgeArticleCommand("Artigo duplicado", "Conteudo.", null));

        await mediator.Send(new LinkKnowledgeArticleDeviceCommand(article.Id, device.Id));

        var action = () => mediator.Send(
            new LinkKnowledgeArticleDeviceCommand(article.Id, device.Id));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O device já está vinculado ao artigo.");
    }

    [Fact]
    public async Task LinkDevice_WithoutOperateRole_ShouldThrowUnauthorizedPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(
            new LinkKnowledgeArticleDeviceCommand(Guid.NewGuid(), Guid.NewGuid()));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: Admin, Operator");
    }
}
