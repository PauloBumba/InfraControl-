using FluentAssertions;
using InfraControl.Application.Notifications.Preferences.Commands;
using InfraControl.Application.Notifications.Preferences.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Notifications;

/// <summary>
/// F9-T003: preferências de notificação ponta-a-ponta — upsert, listagem,
/// 404 pt-BR, exclusão e autorização (ADR-016).
/// </summary>
public sealed class NotificationPreferenceIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedUserAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InfraControl.Infrastructure.Persistence.AppDbContext>();
        var user = User.Create(Guid.NewGuid(), email, "Usuário Preferências");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task SetAndSearchPreference_ShouldPersistAsOperator()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Operator);
        var userId = await SeedUserAsync(Services, "pref1@exemplo.test");

        var result = await mediator.Send(new SetNotificationPreferenceCommand(
            userId, NotificationChannel.Email, Enabled: false));
        result.Enabled.Should().BeFalse();

        await mediator.Send(new SetNotificationPreferenceCommand(
            userId, NotificationChannel.Email, Enabled: true));

        var preferences = await mediator.Send(
            new SearchNotificationPreferencesQuery(userId));
        preferences.Should().ContainSingle();
        preferences.Single().Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task DeletePreference_ShouldRemoveAndThenFailWith404()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var userId = await SeedUserAsync(Services, "pref2@exemplo.test");

        await mediator.Send(new SetNotificationPreferenceCommand(
            userId, NotificationChannel.SignalR, Enabled: true));
        await mediator.Send(new DeleteNotificationPreferenceCommand(
            userId, NotificationChannel.SignalR));

        var action = () => mediator.Send(new DeleteNotificationPreferenceCommand(
            userId, NotificationChannel.SignalR));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Preferência de notificação 'SignalR' do usuário '*' não encontrada.");
    }

    [Fact]
    public async Task Set_UnknownUser_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new SetNotificationPreferenceCommand(
            unknown, NotificationChannel.Email, Enabled: true));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Set_WithViewerRole_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Viewer);

        var action = () => mediator.Send(new SetNotificationPreferenceCommand(
            Guid.NewGuid(), NotificationChannel.Email, Enabled: true));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Roles ausentes: *");
    }

    [Fact]
    public async Task Search_WithViewerRole_ShouldSucceed()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Viewer);
        var userId = await SeedUserAsync(Services, "pref3@exemplo.test");

        var preferences = await mediator.Send(
            new SearchNotificationPreferencesQuery(userId));

        preferences.Should().BeEmpty();
    }
}
