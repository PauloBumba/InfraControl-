using System.Security.Cryptography;
using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Sessions;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F1-T016b / ADR-020: o usuário enxerga e administra as PRÓPRIAS sessões.
/// Cada revogação escreve o check "Revoked" no cache compartilhado — é o que
/// derruba o cookie de OUTRA instância na hora, sem esperar o CheckInterval
/// (a janela de 30s da ADR-018 era o que ficou pendente para cá).
/// </summary>
public sealed class SessionManagementTests
{
    private const string StrongPassword = "ChangeMe!123456";

    [Fact]
    public async Task List_Should_Return_Only_Own_Active_Sessions_With_Current_Flag()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (userA, emailA) = await SeedActiveUserAsync(scope, "sessions-a@example.com");
        var (userB, emailB) = await SeedActiveUserAsync(scope, "sessions-b@example.com");
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();

        var sid1 = await LoginAsync(scope, emailA);
        var sid2 = await LoginAsync(scope, emailA);
        var sid3 = await LoginAsync(scope, emailA);
        var sidB = await LoginAsync(scope, emailB);

        (await sessions.RevokeAsync(userA, sid3)).Succeeded.Should().BeTrue();
        var list = await sessions.ListAsync(userA, sid1);

        list.Select(item => item.Id).Should().BeEquivalentTo([sid1, sid2],
            "lista só as ativas do dono: a revogada sai e a sessão do outro usuário nunca entra");
        list.Single(item => item.Id == sid1).IsCurrent.Should().BeTrue("a sessão do cookie é a atual");
        list.Single(item => item.Id == sid2).IsCurrent.Should().BeFalse();
        list.Select(item => item.Id).Should().NotContain(sidB);
    }

    [Fact]
    public async Task Revoke_Should_Revoked_The_Session_And_Write_The_Check_To_The_Shared_Cache()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (userId, email) = await SeedActiveUserAsync(scope, "revoke-one@example.com");
        var sid = await LoginAsync(scope, email);
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var cache = (FakeCacheService)scope.ServiceProvider.GetRequiredService<ICacheService>();

        var result = await sessions.RevokeAsync(userId, sid);

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await dbContext.UserSessions.SingleAsync(item => item.Id == sid);
        row.RevokedAt.Should().NotBeNull();
        row.IsActive.Should().BeFalse();

        var check = cache.CheckAt(ActiveSessionCache.Key(sid));
        check.Should().NotBeNull("sem o check no cache, outra instância só percebe daqui a CheckInterval");
        check!.Status.Should().Be(ActiveSessionStatus.Revoked);
    }

    [Fact]
    public async Task Revoke_Someone_Elses_Session_Should_Fail_As_Not_Found()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (userA, _) = await SeedActiveUserAsync(scope, "revoke-alheia-a@example.com");
        var (_, emailB) = await SeedActiveUserAsync(scope, "revoke-alheia-b@example.com");
        var sidB = await LoginAsync(scope, emailB);
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var cache = (FakeCacheService)scope.ServiceProvider.GetRequiredService<ICacheService>();

        var result = await sessions.RevokeAsync(userA, sidB);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Equal(["Sessão não encontrada."],
            "a resposta é a mesma para inexistente e para alheia — o cliente não vira oráculo do sid");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.UserSessions.SingleAsync(item => item.Id == sidB)).RevokedAt.Should().BeNull();
        cache.CheckAt(ActiveSessionCache.Key(sidB)).Should().BeNull("nada foi revogado, nada se escreve no cache");
    }

    [Fact]
    public async Task RevokeOthers_Should_Keep_The_Current_And_Revogue_The_Rest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (userId, email) = await SeedActiveUserAsync(scope, "revoke-others@example.com");
        var sid1 = await LoginAsync(scope, email);
        var sid2 = await LoginAsync(scope, email);
        var sid3 = await LoginAsync(scope, email);
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var cache = (FakeCacheService)scope.ServiceProvider.GetRequiredService<ICacheService>();

        var result = await sessions.RevokeOthersAsync(userId, sid1);

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.UserSessions.SingleAsync(item => item.Id == sid1)).RevokedAt.Should()
            .BeNull("sair de todos os OUTROS nunca derruba a sessão de onde o comando partiu");
        (await dbContext.UserSessions.CountAsync(item => item.Id != sid1 && item.RevokedAt != null))
            .Should().Be(2, "as outras duas sessões morrem");

        cache.CheckAt(ActiveSessionCache.Key(sid1)).Should().BeNull("a atual não é tocada");
        cache.CheckAt(ActiveSessionCache.Key(sid2))!.Status.Should().Be(ActiveSessionStatus.Revoked);
        cache.CheckAt(ActiveSessionCache.Key(sid3))!.Status.Should().Be(ActiveSessionStatus.Revoked);
    }

    [Fact]
    public async Task RevokeOthers_Without_A_Current_Session_Should_Fail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (userId, email) = await SeedActiveUserAsync(scope, "revoke-others-nc@example.com");
        var sid = await LoginAsync(scope, email);
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();

        var result = await sessions.RevokeOthersAsync(userId, currentSessionId: null);

        result.Succeeded.Should().BeFalse("sem saber qual é a atual, revogar 'as outras' apagaria tudo — inclusive esta");
        result.Errors.Should().Equal("Sessão atual não identificada.");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.UserSessions.SingleAsync(item => item.Id == sid)).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Login_Should_Cap_The_Absolute_Lifetime_By_User_Role()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        // usuário registrado nasce Viewer: o cap da role encurta o teto de 12h
        await using var provider = BuildProvider(connection, options =>
        {
            options.AbsoluteLifetime = TimeSpan.FromHours(12);
            options.RoleAbsoluteLifetimes = new Dictionary<string, TimeSpan> { ["Viewer"] = TimeSpan.FromHours(1) };
        });
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (_, email) = await SeedActiveUserAsync(scope, "role-cap@example.com");
        var sid = await LoginAsync(scope, email);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await dbContext.UserSessions.SingleAsync(item => item.Id == sid);
        (row.AbsoluteExpiresAt - row.CreatedAt).Should().BeCloseTo(TimeSpan.FromHours(1), TimeSpan.FromSeconds(5),
            "o cap da role vale no login");
        (row.ExpiresAt - row.CreatedAt).Should().BeCloseTo(TimeSpan.FromDays(30), TimeSpan.FromSeconds(5),
            "o deslizante não é afetado pelo cap");
    }

    [Fact]
    public async Task Login_Without_A_Role_Cap_Should_Use_The_Configured_Absolute_Lifetime()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        // sem cap de role: o teto vem da configuração (2h), não de constante de 12h
        await using var provider = BuildProvider(connection, options =>
            options.AbsoluteLifetime = TimeSpan.FromHours(2));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (_, email) = await SeedActiveUserAsync(scope, "configured-ceiling@example.com");
        var sid = await LoginAsync(scope, email);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await dbContext.UserSessions.SingleAsync(item => item.Id == sid);
        (row.AbsoluteExpiresAt - row.CreatedAt).Should().BeCloseTo(TimeSpan.FromHours(2), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Logout_Should_Write_The_Revoked_Check_To_The_Shared_Cache()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (userId, email) = await SeedActiveUserAsync(scope, "logout-cache@example.com");
        var sid = await LoginAsync(scope, email);
        var cache = (FakeCacheService)scope.ServiceProvider.GetRequiredService<ICacheService>();
        var authService = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        await authService.LogoutAsync(userId, refreshToken: null);

        var check = cache.CheckAt(ActiveSessionCache.Key(sid));
        check.Should().NotBeNull("logout também é revogação: o check vai para o cache compartilhado");
        check!.Status.Should().Be(ActiveSessionStatus.Revoked);
    }

    private static async Task<(Guid UserId, string Email)> SeedActiveUserAsync(IServiceScope scope, string email)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var register = await service.RegisterAsync(new RegisterUserRequest(email, "Sessions User", StrongPassword));
        register.Succeeded.Should().BeTrue(string.Join("; ", register.Errors));

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.DomainUsers.SingleAsync(item => item.Email == email);
        user.Enable();
        await dbContext.SaveChangesAsync();
        return (user.Id, email);
    }

    private static async Task<Guid> LoginAsync(IServiceScope scope, string email)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var outcome = await service.LoginAsync(new LoginRequest(email, StrongPassword));
        outcome.Failure.Should().BeNull();
        return outcome.Authentication!.User.SessionId;
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection, Action<SessionLifetimeOptions>? configureSession = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<InfraControlUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<ICacheService, FakeCacheService>();
        services.AddScoped<IIdentityEmailSender, FakeEmailSender>();
        services.AddScoped<IGoogleIdentityTokenValidator, NotConfiguredGoogleValidator>();
        services.Configure<CredentialEncryptionOptions>(options =>
            options.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        services.AddScoped<ICredentialProtector, AesGcmCredentialProtector>();

        // F1-T016b: o serviço de login passa a ler os prazos das options
        // (antes: constantes fixas) e o SessionService cuida da lista/revogação.
        if (configureSession is null)
            services.AddOptions<SessionLifetimeOptions>();
        else
            services.Configure(configureSession);

        // F1-T016c: os serviços de identidade gravam audit direto (fora do
        // pipeline MediatR) — a porta precisa estar no harness.
        services.AddScoped<IAuditWriter, InfraControl.Infrastructure.Repositories.AuditWriter>();
        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<ISessionService, SessionService>();
        return services.BuildServiceProvider();
    }

    private sealed class NotConfiguredGoogleValidator : IGoogleIdentityTokenValidator
    {
        public Task<GoogleTokenValidation> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
            => Task.FromResult(GoogleTokenValidation.NotConfigured());
    }

    private sealed class FakeEmailSender : IIdentityEmailSender
    {
        public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();

        public ActiveSessionCheck? CheckAt(string key)
            => _store.TryGetValue(key, out var value) ? value as ActiveSessionCheck : null;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(key, out var value) && value is T typed ? typed : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.ContainsKey(key));

        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            if (_store.ContainsKey(key)) return Task.FromResult(false);
            _store[key] = true;
            return Task.FromResult(true);
        }
    }
}
