using FluentAssertions;
using InfraControl.Application.Account.Settings.Commands;
using InfraControl.Application.Account.Settings.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Account;

/// <summary>
/// Fase 06d: UserSettings — lazy-create, update, validação de timezone,
/// 400 pt-BR, query devolvendo defaults sem gravar.
/// </summary>
public sealed class UserSettingsCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public UserSettingsCommandTests()
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
        var user = User.Create(Guid.NewGuid(), "settings@exemplo.test", "Settings");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return (context, user.Id);
    }

    private static UpdateUserSettingsCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new UserSettingsRepository(context),
            NullLogger<UpdateUserSettingsCommandHandler>.Instance);

    private static GetUserSettingsQueryHandler QueryHandler(AppDbContext context)
        => new(new UserSettingsRepository(context));

    [Fact]
    public async Task Update_FirstTime_ShouldLazyCreate()
    {
        var (context, userId) = await CreateAsync();

        var result = await UpdateHandler(context).Handle(
            new UpdateUserSettingsCommand(
                userId, "America/New_York", DarkMode: false, AutoRefresh: false, Telemetry: true),
            CancellationToken.None);

        result.UserId.Should().Be(userId);
        result.Timezone.Should().Be("America/New_York");
        result.DarkMode.Should().BeFalse();
        result.AutoRefresh.Should().BeFalse();
        result.Telemetry.Should().BeTrue();

        var saved = await context.UserSettings.AsNoTracking()
            .SingleAsync(s => s.UserId == userId);
        saved.Timezone.Should().Be("America/New_York");
    }

    [Fact]
    public async Task Update_SecondTime_ShouldOverwrite()
    {
        var (context, userId) = await CreateAsync();
        await UpdateHandler(context).Handle(
            new UpdateUserSettingsCommand(
                userId, "America/New_York", false, false, true),
            CancellationToken.None);

        var result = await UpdateHandler(context).Handle(
            new UpdateUserSettingsCommand(
                userId, "America/Sao_Paulo", true, true, false),
            CancellationToken.None);

        result.Timezone.Should().Be("America/Sao_Paulo");
        result.DarkMode.Should().BeTrue();
        result.Telemetry.Should().BeFalse();
        (await context.UserSettings.AsNoTracking().CountAsync(s => s.UserId == userId))
            .Should().Be(1);
    }

    [Fact]
    public async Task Update_EmptyTimezone_ShouldThrowArgumentExceptionPtBr()
    {
        var (context, userId) = await CreateAsync();

        var act = async () => await UpdateHandler(context).Handle(
            new UpdateUserSettingsCommand(
                userId, "   ", DarkMode: true, AutoRefresh: true, Telemetry: false),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O fuso horário é obrigatório.*");
    }

    [Fact]
    public async Task Query_WithoutStoredSettings_ShouldReturnDefaultsWithoutPersisting()
    {
        var (context, userId) = await CreateAsync();

        var result = await QueryHandler(context).Handle(
            new GetUserSettingsQuery(userId), CancellationToken.None);

        result.Timezone.Should().Be("America/Sao_Paulo");
        result.DarkMode.Should().BeTrue();
        result.AutoRefresh.Should().BeTrue();
        result.Telemetry.Should().BeFalse();

        (await context.UserSettings.AsNoTracking().CountAsync(s => s.UserId == userId))
            .Should().Be(0);
    }

    [Fact]
    public async Task Query_AfterUpdate_ShouldReturnPersistedValues()
    {
        var (context, userId) = await CreateAsync();
        await UpdateHandler(context).Handle(
            new UpdateUserSettingsCommand(
                userId, "America/New_York", false, false, true),
            CancellationToken.None);

        var result = await QueryHandler(context).Handle(
            new GetUserSettingsQuery(userId), CancellationToken.None);

        result.Timezone.Should().Be("America/New_York");
        result.DarkMode.Should().BeFalse();
        result.Telemetry.Should().BeTrue();
    }
}
