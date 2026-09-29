using FluentAssertions;
using InfraControl.Application.Notifications.Recipients.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Notifications;

/// <summary>
/// F9-T002: destinatários de notificação ponta-a-ponta — leitura, 404
/// pt-BR e autorização por roles (ADR-016).
/// </summary>
public sealed class NotificationRecipientIntegrationTests : BaseIntegrationTest
{
    private static async Task<(Guid NotificationId, Guid UserId)> SeedAsync(
        IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Usuário criado", "Mensagem.");
        context.Notifications.Add(notification);

        // Usuário já nasce na role destinatária (Operator) — dedup do
        // teste ResolveUsers coberto sem reabrir o contexto.
        var user = User.Create(Guid.NewGuid(), "destinatario@exemplo.test", "Destinatário");
        user.AssignRole(UserRole.Operator);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();

        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(notification.Id, user.Id));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(notification.Id, UserRole.Operator));
        await context.SaveChangesAsync();

        return (notification.Id, user.Id);
    }

    [Fact]
    public async Task GetRecipients_WithViewerRole_ShouldListDestinatarios()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Viewer);
        var (notificationId, userId) = await SeedAsync(Services);

        var page = await mediator.Send(new GetNotificationRecipientsQuery(notificationId));

        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle(r => r.UserId == userId);
        page.Items.Should().ContainSingle(r => r.Role == "Operator");
    }

    [Fact]
    public async Task ResolveUsers_ShouldDeduplicateAcrossRoles()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var (notificationId, userId) = await SeedAsync(Services);

        // Usuário é destinatário direto E está na role destinatária —
        // dedup garante uma única notificação lógica (docs/07).
        var resolved = await mediator.Send(new ResolveNotificationUsersQuery(notificationId));

        resolved.Should().ContainSingle().Which.Should().Be(userId);
    }

    [Fact]
    public async Task GetRecipients_UnknownNotification_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new GetNotificationRecipientsQuery(unknown));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Notificação '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task GetRecipients_WithoutRoles_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoRoles();

        var action = () => mediator.Send(new GetNotificationRecipientsQuery(Guid.NewGuid()));

        var ex = await action.Should().ThrowAsync<UnauthorizedAccessException>();
        ex.WithMessage("Roles ausentes: *");
    }
}
