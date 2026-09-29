using FluentAssertions;
using InfraControl.Application.Identity.Sessions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F1-T016b / ADR-020: o leitor é o que dá autoridade ao cookie — e agora
/// também é quem mantém o <c>LastSeenAt</c> viva, sob throttle, para a lista
/// de sessões mostrar "visto por último" de verdade (sem isto o dashboard,
/// que nunca chama refresh, mostraria sempre a hora do login).
///
/// Os estados vencidos são fabricados por SQL puro de propósito: os setters da
/// entidade são privados e o domínio PROÍBE criar sessão já expirada — o teste
/// não deve conseguir burlar essa regra pelo construtor.
/// </summary>
public sealed class ActiveSessionReaderTests
{
    [Fact]
    public async Task Active_Session_Should_Check_Active_And_Touch_A_Stale_LastSeen()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var (userId, sessionId) = await SeedSessionAsync(dbContext);
        await BackdateAsync(dbContext, sessionId, lastSeenAt: DateTime.UtcNow.AddMinutes(-10));

        var reader = new ActiveSessionReader(dbContext);
        var check = await reader.CheckAsync(sessionId, userId);

        check.Status.Should().Be(ActiveSessionStatus.Active);
        var row = await dbContext.UserSessions.AsNoTracking().SingleAsync(item => item.Id == sessionId);
        row.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1),
            "sessão ativa com LastSeenAt velho é tocada — é isso que a lista mostra como 'visto por último'");
    }

    [Fact]
    public async Task Active_Session_Should_Not_Touch_A_Recent_LastSeen()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var (userId, sessionId) = await SeedSessionAsync(dbContext);
        var before = await dbContext.UserSessions.AsNoTracking().SingleAsync(item => item.Id == sessionId);

        var reader = new ActiveSessionReader(dbContext);
        var check = await reader.CheckAsync(sessionId, userId);

        check.Status.Should().Be(ActiveSessionStatus.Active);
        var after = await dbContext.UserSessions.AsNoTracking().SingleAsync(item => item.Id == sessionId);
        after.LastSeenAt.Should().Be(before.LastSeenAt,
            "dentro do throttle não há escrita: o custo do toque é um UPDATE a cada 5min, não por request");
    }

    [Fact]
    public async Task Revoked_Session_Should_Check_Revoked_Without_Touching()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var (userId, sessionId) = await SeedSessionAsync(dbContext);

        var session = await dbContext.UserSessions.SingleAsync(item => item.Id == sessionId);
        session.Revoke();
        await dbContext.SaveChangesAsync();
        var stale = DateTime.UtcNow.AddMinutes(-10);
        await BackdateAsync(dbContext, sessionId, lastSeenAt: stale);

        var reader = new ActiveSessionReader(dbContext);
        var check = await reader.CheckAsync(sessionId, userId);

        check.Status.Should().Be(ActiveSessionStatus.Revoked);
        var row = await dbContext.UserSessions.AsNoTracking().SingleAsync(item => item.Id == sessionId);
        row.LastSeenAt.Should().Be(stale, "só sessão ativa é tocada — morta não ganha vida nova na lista");
    }

    [Fact]
    public async Task Sliding_Expired_Session_Should_Check_SlidingExpired()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var (userId, sessionId) = await SeedSessionAsync(dbContext);
        await BackdateAsync(dbContext, sessionId, expiresAt: DateTime.UtcNow.AddHours(-1));

        var reader = new ActiveSessionReader(dbContext);
        (await reader.CheckAsync(sessionId, userId)).Status.Should().Be(ActiveSessionStatus.SlidingExpired);
    }

    [Fact]
    public async Task Absolute_Expired_Session_Should_Check_AbsoluteExpired()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var (userId, sessionId) = await SeedSessionAsync(dbContext);
        await BackdateAsync(dbContext, sessionId,
            expiresAt: DateTime.UtcNow.AddDays(10),
            absoluteExpiresAt: DateTime.UtcNow.AddHours(-1));

        var reader = new ActiveSessionReader(dbContext);
        (await reader.CheckAsync(sessionId, userId)).Status.Should().Be(ActiveSessionStatus.AbsoluteExpired);
    }

    [Fact]
    public async Task Session_Of_Another_User_Should_Check_WrongUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        var (_, sessionId) = await SeedSessionAsync(dbContext);

        var reader = new ActiveSessionReader(dbContext);
        (await reader.CheckAsync(sessionId, Guid.NewGuid())).Status.Should().Be(ActiveSessionStatus.WrongUser);
    }

    [Fact]
    public async Task Unknown_Session_Should_Check_NotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        await SeedSessionAsync(dbContext);

        var reader = new ActiveSessionReader(dbContext);
        (await reader.CheckAsync(Guid.NewGuid(), Guid.NewGuid())).Status.Should().Be(ActiveSessionStatus.NotFound);
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static async Task<(Guid UserId, Guid SessionId)> SeedSessionAsync(AppDbContext dbContext)
    {
        var user = User.CreatePending(
            Guid.NewGuid(), $"reader-{Guid.NewGuid():N}@example.com", "Reader User", [UserRole.Viewer]);
        var session = UserSession.Start(user.Id, "hash", TimeSpan.FromDays(30), TimeSpan.FromHours(12));
        dbContext.DomainUsers.Add(user);
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();
        return (user.Id, session.Id);
    }

    /// <summary>Estados que o domínio não deixa criar ficam por SQL — setter privado é regra, não acidente.</summary>
    private static async Task BackdateAsync(
        AppDbContext dbContext,
        Guid sessionId,
        DateTime? lastSeenAt = null,
        DateTime? expiresAt = null,
        DateTime? absoluteExpiresAt = null)
    {
        if (lastSeenAt is not null)
            await dbContext.Database.ExecuteSqlAsync(
                $"""UPDATE "UserSessions" SET "LastSeenAt" = {lastSeenAt} WHERE "Id" = {sessionId}""");
        if (expiresAt is not null)
            await dbContext.Database.ExecuteSqlAsync(
                $"""UPDATE "UserSessions" SET "ExpiresAt" = {expiresAt} WHERE "Id" = {sessionId}""");
        if (absoluteExpiresAt is not null)
            await dbContext.Database.ExecuteSqlAsync(
                $"""UPDATE "UserSessions" SET "AbsoluteExpiresAt" = {absoluteExpiresAt} WHERE "Id" = {sessionId}""");
    }
}
