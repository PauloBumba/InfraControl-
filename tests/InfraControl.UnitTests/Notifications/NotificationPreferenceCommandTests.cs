using FluentAssertions;
using InfraControl.Application.Notifications.Preferences.Commands;
using InfraControl.Application.Notifications.Preferences.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T003: preferências de notificação — upsert, 404 (usuário e
/// preferência), 400 (canal inválido), listagem por canal.
/// </summary>
public sealed class NotificationPreferenceCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationPreferenceCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, Guid UserId)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var user = User.Create(Guid.NewGuid(), "preferencias@exemplo.test", "Preferências");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return (context, user.Id);
    }

    private static SetNotificationPreferenceCommandHandler SetHandler(AppDbContext context)
        => new(
            new NotificationPreferenceRepository(context),
            NullLogger<SetNotificationPreferenceCommandHandler>.Instance);

    private static DeleteNotificationPreferenceCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new NotificationPreferenceRepository(context),
            NullLogger<DeleteNotificationPreferenceCommandHandler>.Instance);

    [Fact]
    public async Task Set_NewPreference_ShouldCreate()
    {
        var (context, userId) = await CreateAsync();

        var result = await SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                userId, NotificationChannel.Email, Enabled: false),
            CancellationToken.None);

        result.UserId.Should().Be(userId);
        result.Channel.Should().Be(NotificationChannel.Email);
        result.Enabled.Should().BeFalse();
        context.NotificationPreferences.Should().ContainSingle();
    }

    [Fact]
    public async Task Set_ExistingPreference_ShouldUpdateWithoutDuplicating()
    {
        var (context, userId) = await CreateAsync();
        await SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                userId, NotificationChannel.Email, Enabled: true),
            CancellationToken.None);

        var updated = await SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                userId, NotificationChannel.Email, Enabled: false),
            CancellationToken.None);

        updated.Enabled.Should().BeFalse();
        context.NotificationPreferences.Should().ContainSingle();
    }

    [Fact]
    public async Task Set_UnknownUser_ShouldThrowKeyNotFoundPtBr()
    {
        var (context, _) = await CreateAsync();
        var unknown = Guid.NewGuid();

        var action = () => SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                unknown, NotificationChannel.SignalR, Enabled: true),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Set_UndefinedChannel_ShouldThrowArgumentExceptionPtBr()
    {
        var (context, userId) = await CreateAsync();

        var action = () => SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                userId, (NotificationChannel)99, Enabled: true),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Canal de notificação inválido: '99'.*");
    }

    [Fact]
    public async Task Delete_ExistingPreference_ShouldRemove()
    {
        var (context, userId) = await CreateAsync();
        await SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                userId, NotificationChannel.Telegram, Enabled: true),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteNotificationPreferenceCommand(userId, NotificationChannel.Telegram),
            CancellationToken.None);

        context.NotificationPreferences.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownPreference_ShouldThrowKeyNotFoundPtBr()
    {
        var (context, userId) = await CreateAsync();

        var action = () => DeleteHandler(context).Handle(
            new DeleteNotificationPreferenceCommand(userId, NotificationChannel.Webhook),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage("Preferência de notificação 'Webhook' do usuário '*' não encontrada.");
    }

    [Fact]
    public async Task Search_ShouldListByChannelOrder()
    {
        var (context, userId) = await CreateAsync();
        await SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                userId, NotificationChannel.Webhook, Enabled: true),
            CancellationToken.None);
        await SetHandler(context).Handle(
            new SetNotificationPreferenceCommand(
                userId, NotificationChannel.Email, Enabled: false),
            CancellationToken.None);

        var result = await new SearchNotificationPreferencesQueryHandler(
                new NotificationPreferenceRepository(context))
            .Handle(new SearchNotificationPreferencesQuery(userId), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(p => p.Channel).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Search_UnknownUser_ShouldThrowKeyNotFoundPtBr()
    {
        var (context, _) = await CreateAsync();
        var unknown = Guid.NewGuid();

        var action = () => new SearchNotificationPreferencesQueryHandler(
                new NotificationPreferenceRepository(context))
            .Handle(new SearchNotificationPreferencesQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Usuário '{unknown}' não encontrado.");
    }
}
