using System.Security.Cryptography;
using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Sessions;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.RemoteAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OtpNet;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F1-T016c: eventos de segurança que NÃO passam pelo pipeline MediatR (login,
/// MFA e sessões são chamadas diretas do controller) entram no audit existente
/// pelo caminho manual — <c>IAuditWriter</c> no MESMO <c>SaveChanges</c> da
/// operação (efeito e audit commitam juntos). Só metadados: action, resource,
/// outcome, actor = e-mail do dono, correlation = TraceId — nenhum código,
/// senha ou secret (o AuditBehavior faz igual).
///
/// Só SUCESSO é auditado: falha não muda estado e o escopo da task é
/// "MFA ativado/desativado, sessão revogada, login com segundo fator".
/// </summary>
public sealed class AuthenticationSecurityAuditTests
{
    private const string StrongPassword = "ChangeMe!123456";

    [Fact]
    public async Task Login_With_Valid_Totp_Should_Audit_LoginWithMfa()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-totp-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var setup = await EnableMfaAsync(scope, userId);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var outcome = await auth.LoginAsync(new LoginRequest(
            email, StrongPassword, ComputeTotp(setup.Secret)));

        outcome.Failure.Should().BeNull();

        var entry = await SingleAuditAsync(scope, "LoginWithMfa");
        entry.Resource.Should().Be("Session");
        entry.Outcome.Should().Be("Succeeded");
        entry.ActorId.Should().Be(email, "o audit é legível: quem passou pelo segundo fator");
        entry.Error.Should().BeNull();
    }

    [Fact]
    public async Task Login_Without_Mfa_Should_Not_Audit_LoginWithMfa()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-plain-{Guid.NewGuid():N}@example.com";
        await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        (await auth.LoginAsync(new LoginRequest(email, StrongPassword))).Failure.Should().BeNull();

        (await AuditAsync(scope, "LoginWithMfa")).Should().BeEmpty(
            "login por senha pura não é login com segundo fator");
    }

    [Fact]
    public async Task Login_With_Invalid_Mfa_Code_Should_Not_Audit_Or_Create_Session()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-badcode-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        await EnableMfaAsync(scope, userId);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var outcome = await auth.LoginAsync(new LoginRequest(email, StrongPassword, "000000"));

        outcome.Authentication.Should().BeNull();
        (await AuditAsync(scope, "LoginWithMfa")).Should().BeEmpty(
            "tentativa falha não vira evento de sucesso no audit");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.UserSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Confirm_Mfa_Should_Audit_MfaEnabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-enable-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);

        await EnableMfaAsync(scope, userId);

        var entry = await SingleAuditAsync(scope, "MfaEnabled");
        entry.Resource.Should().Be("Mfa");
        entry.Outcome.Should().Be("Succeeded");
        entry.ActorId.Should().Be(email);
    }

    [Fact]
    public async Task Confirm_Mfa_With_Invalid_Code_Should_Not_Audit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-enable-bad-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var setup = await service.SetupMfaAsync(userId);

        var confirm = await service.ConfirmMfaAsync(userId, "000000");

        confirm.Succeeded.Should().BeFalse("código errado não liga o MFA");
        setup.Should().NotBeNull();
        (await AuditAsync(scope, "MfaEnabled")).Should().BeEmpty(
            "código errado não vira evento de sucesso");
    }

    [Fact]
    public async Task Disable_Mfa_Should_Audit_MfaDisabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-disable-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var setup = await EnableMfaAsync(scope, userId);
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await service.DisableMfaAsync(
            userId, ComputeTotp(setup.Secret), password: null);

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var entry = await SingleAuditAsync(scope, "MfaDisabled");
        entry.Resource.Should().Be("Mfa");
        entry.Outcome.Should().Be("Succeeded");
        entry.ActorId.Should().Be(email);
    }

    [Fact]
    public async Task Revoke_One_Session_Should_Audit_SessionRevoked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-revoke-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var sessionId = await LoginAsync(scope, email);
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();

        var result = await sessions.RevokeAsync(userId, sessionId);

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var entry = await SingleAuditAsync(scope, "SessionRevoked");
        entry.Resource.Should().Be("Session");
        entry.Outcome.Should().Be("Succeeded");
        entry.ActorId.Should().Be(email);
    }

    [Fact]
    public async Task Revoke_Others_Should_Audit_SessionsRevoked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-revoke-others-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var current = await LoginAsync(scope, email);
        await LoginAsync(scope, email);
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();

        var result = await sessions.RevokeOthersAsync(userId, current);

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var entry = await SingleAuditAsync(scope, "SessionsRevoked");
        entry.Resource.Should().Be("Session");
        entry.ActorId.Should().Be(email);
    }

    [Fact]
    public async Task Logout_Should_Audit_Logout()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-logout-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        await LoginAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        await auth.LogoutAsync(userId, refreshToken: null);

        var entry = await SingleAuditAsync(scope, "Logout");
        entry.Resource.Should().Be("Session");
        entry.Outcome.Should().Be("Succeeded");
        entry.ActorId.Should().Be(email);
    }

    [Fact]
    public async Task Change_Password_Should_Audit_SessionsRevoked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var email = $"audit-chpw-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        await LoginAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await auth.ChangePasswordAsync(new ChangePasswordRequest(
            userId, StrongPassword, "NovaSenha!987654"));

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var entry = await SingleAuditAsync(scope, "SessionsRevoked");
        entry.Resource.Should().Be("Session");
        entry.ActorId.Should().Be(email,
            "trocar senha derruba as sessões — e o audit registra quem derrubou");
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static string ComputeTotp(string base32Secret)
        => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    private static async Task<List<AuditEntry>> AuditAsync(IServiceScope scope, string action)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.AuditEntries.AsNoTracking()
            .Where(entry => entry.Action == action)
            .OrderBy(entry => entry.OccurredAt)
            .ToListAsync();
    }

    private static async Task<AuditEntry> SingleAuditAsync(IServiceScope scope, string action)
    {
        var entries = await AuditAsync(scope, action);
        entries.Should().HaveCount(1, $"esperava exatamente um registro de '{action}'");
        return entries[0];
    }

    private static async Task<Guid> SeedActiveUserAsync(IServiceScope scope, string email)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var register = await service.RegisterAsync(new RegisterUserRequest(email, "Audit User", StrongPassword));
        register.Succeeded.Should().BeTrue(string.Join("; ", register.Errors));

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.DomainUsers.SingleAsync(item => item.Email == email);
        user.Enable();
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Setup + confirm reais; devolve o secret.</summary>
    private static async Task<MfaSetupResult> EnableMfaAsync(IServiceScope scope, Guid userId)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var setup = await service.SetupMfaAsync(userId);
        var confirm = await service.ConfirmMfaAsync(userId, ComputeTotp(setup.Secret));
        confirm.Succeeded.Should().BeTrue(string.Join("; ", confirm.Errors));
        return setup;
    }

    private static async Task<Guid> LoginAsync(IServiceScope scope, string email)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var outcome = await service.LoginAsync(new LoginRequest(email, StrongPassword));
        outcome.Failure.Should().BeNull();
        return outcome.Authentication!.User.SessionId;
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
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
        services.AddOptions<SessionLifetimeOptions>();
        // F1-T016c: o audit é gravado pelos próprios serviços de identidade (fora
        // do pipeline MediatR) — a porta precisa estar registrada no harness.
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

        public Task SendInvitationAsync(string email, string acceptUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();

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
