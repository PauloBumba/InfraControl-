using FluentAssertions;
using InfraControl.Application.Notifications.Feed.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Notifications;

/// <summary>
/// F18-T001: feed de notificações ponta-a-ponta — pipeline MediatR real
/// (PostgreSQL), shape do `PagedResult` que o front consome e gate de
/// roles da ADR-016 (sem papel → UnauthorizedAccessException).
/// </summary>
public sealed class NotificationFeedIntegrationTests : BaseIntegrationTest
{
    private static async Task SeedAsync(
        AppDbContext context,
        Guid directUserId,
        UserRole? recipientRole = null)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "IncidentCreatedDomainEvent",
            "Notificação do feed", "Mensagem do feed.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        if (directUserId != Guid.Empty)
            context.NotificationRecipients.Add(
                NotificationRecipient.CreateForUser(notification.Id, directUserId));

        if (recipientRole is not null)
            context.NotificationRecipients.Add(
                NotificationRecipient.CreateForRole(notification.Id, recipientRole.Value));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Feed_ShouldReturnPagedResultOfTheCurrentUser()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UseRole(UserRole.Viewer);
        await SeedAsync(db, CurrentUser.UserId!.Value);

        var page = await mediator.Send(new GetNotificationFeedQuery());

        page.Page.Should().Be(1);
        page.PageSize.Should().Be(20);
        page.TotalCount.Should().Be(1);

        var item = page.Items.Single();
        item.Title.Should().Be("Notificação do feed");
        item.EventType.Should().Be("IncidentCreatedDomainEvent");
        item.Message.Should().NotBeNullOrWhiteSpace();
        item.CreatedAt.Should().NotBe(default(DateTime));
        item.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Feed_ShouldResolveRoleRecipient_WithoutDuplicates()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UseRole(UserRole.Operator);

        // Destinatário direto (usuário atual) + role do mesmo usuário
        // → uma única notificação lógica (dedup, docs/07).
        await SeedAsync(db, CurrentUser.UserId!.Value, UserRole.Operator);

        var page = await mediator.Send(new GetNotificationFeedQuery());

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Feed_WithoutRoles_ShouldBeBlockedByAuthorizationBehavior()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(new GetNotificationFeedQuery());

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
