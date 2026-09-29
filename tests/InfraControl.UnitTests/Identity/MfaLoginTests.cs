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
using OtpNet;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F1-T016a (ADR-019): login com MFA — a senha continua sendo validada PRIMEIRO;
/// só depois o código TOTP (ou um recovery code de uso único) destrava a sessão.
/// Falha de MFA não cria sessão e tem mensagem própria da UI (diferente de
/// "Credenciais inválidas.").
/// </summary>
public sealed class MfaLoginTests
{
    private const string StrongPassword = "ChangeMe!123456";
    private const string InvalidTotp = "000000";

    [Fact]
    public async Task Login_EnabledWithoutCode_ShouldFailMfaRequiredWithoutSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "login-required@example.com");
        await EnableMfaAsync(scope, userId);
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var outcome = await service.LoginAsync(new LoginRequest("login-required@example.com", StrongPassword));

        outcome.Authentication.Should().BeNull("sem código não há sessão");
        outcome.Failure.Should().Be(LoginFailure.MfaRequired);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.UserSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Login_EnabledWithValidTotp_ShouldCreateSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "login-valid@example.com");
        var secret = (await EnableMfaAsync(scope, userId)).Secret;
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var outcome = await service.LoginAsync(new LoginRequest(
            "login-valid@example.com", StrongPassword, ComputeTotp(secret)));

        outcome.Failure.Should().BeNull();
        outcome.Authentication.Should().NotBeNull();
        outcome.Authentication!.User.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Login_EnabledWithInvalidCode_ShouldFailMfaCodeInvalid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "login-invalid@example.com");
        await EnableMfaAsync(scope, userId);
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var outcome = await service.LoginAsync(new LoginRequest(
            "login-invalid@example.com", StrongPassword, InvalidTotp));

        outcome.Authentication.Should().BeNull();
        outcome.Failure.Should().Be(LoginFailure.MfaCodeInvalid);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldFailInvalidCredentials_EvenWithValidMfaCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "login-wrongpw@example.com");
        var secret = (await EnableMfaAsync(scope, userId)).Secret;
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var outcome = await service.LoginAsync(new LoginRequest(
            "login-wrongpw@example.com", "OutraSenha!999", ComputeTotp(secret)));

        outcome.Authentication.Should().BeNull();
        outcome.Failure.Should().Be(LoginFailure.InvalidCredentials,
            "senha errada continua sendo rejeitada antes de qualquer checagem de MFA");
    }

    [Fact]
    public async Task Login_WithRecoveryCode_ShouldWorkExactlyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "login-recovery@example.com");
        var enabled = await EnableMfaAsync(scope, userId);
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var code = enabled.RecoveryCodes.First();

        var first = await service.LoginAsync(new LoginRequest(
            "login-recovery@example.com", StrongPassword, code));
        first.Authentication.Should().NotBeNull(
            "recovery code válido deve destravar o login");

        var second = await service.LoginAsync(new LoginRequest(
            "login-recovery@example.com", StrongPassword, code));
        second.Authentication.Should().BeNull("recovery code é de uso único");
        second.Failure.Should().Be(LoginFailure.MfaCodeInvalid);
    }

    [Fact]
    public async Task Login_SetupButNotConfirmed_ShouldNotRequireCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "login-pending@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.SetupMfaAsync(userId);

        var outcome = await service.LoginAsync(new LoginRequest("login-pending@example.com", StrongPassword));

        outcome.Failure.Should().BeNull("setup incompleto (enabled=false) não muda o login");
        outcome.Authentication.Should().NotBeNull();
    }

    private static string ComputeTotp(string base32Secret)
        => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    /// <summary>Setup + confirm reais; devolve secret e os 10 recovery codes.</summary>
    private static async Task<(string Secret, IReadOnlyCollection<string> RecoveryCodes)> EnableMfaAsync(
        IServiceScope scope, Guid userId)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var setup = await service.SetupMfaAsync(userId);
        var confirm = await service.ConfirmMfaAsync(userId, ComputeTotp(setup.Secret));
        confirm.Succeeded.Should().BeTrue(string.Join("; ", confirm.Errors));
        return (setup.Secret, confirm.RecoveryCodes);
    }

    private static async Task<Guid> SeedActiveUserAsync(IServiceScope scope, string email)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var register = await service.RegisterAsync(new RegisterUserRequest(email, "MFA User", StrongPassword));
        register.Succeeded.Should().BeTrue();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.DomainUsers.SingleAsync(u => u.Email == email);
        user.Enable();
        await dbContext.SaveChangesAsync();
        return user.Id;
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
        // F1-T016b: os prazos de sessão agora vêm das options (antes: constantes).
        services.AddOptions<SessionLifetimeOptions>();
        // F1-T016c: os serviços de identidade gravam audit direto (fora do
        // pipeline MediatR) — a porta precisa estar no harness.
        services.AddScoped<IAuditWriter, InfraControl.Infrastructure.Repositories.AuditWriter>();
        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
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
