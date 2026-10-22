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
using System.Security.Cryptography;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F38 — campo <c>User.FullName</c> (coluna nova, opcional) no auto-serviço de
/// perfil.
///
/// O risco real aqui é o DEFAULT do domínio: <c>User.Update(displayName, email,
/// fullName = null)</c> grava <c>null</c> quando o chamador não passa o
/// terceiro argumento. Como <c>UpdateProfileAsync</c> é o caminho de quem só
/// mexe no nome exibido, um "esquecimento" do <c>??</c> apagaria o nome
/// completo de todo mundo em silêncio — é essa regressão que os testes
/// primeiro tranca.
///
/// Semântica do contrato: <c>null</c> = não informado (preserva), <c>""</c> =
/// limpa, valor = grava.
/// </summary>
public sealed class UpdateProfileFullNameTests
{
    private const string StrongPassword = "ChangeMe!123456";

    [Fact]
    public async Task UpdateProfile_Without_FullName_Should_Preserve_The_Stored_Value()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"profile-keep-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        (await auth.UpdateProfileAsync(
            new UpdateProfileRequest(userId, "Nome Antigo", "Ana Maria da Silva"))).Succeeded.Should().BeTrue();

        // Só o nome exibido muda: FullName ausente no contrato (null).
        var result = await auth.UpdateProfileAsync(new UpdateProfileRequest(userId, "Nome Novo"));

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .DomainUsers.AsNoTracking().SingleAsync(item => item.Id == userId);

        user.DisplayName.Should().Be("Nome Novo");
        user.FullName.Should().Be("Ana Maria da Silva",
            "trocar só o nome exibido não pode apagar o nome completo (default null do domínio)");
    }

    [Fact]
    public async Task UpdateProfile_With_FullName_Should_Store_It()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"profile-set-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await auth.UpdateProfileAsync(
            new UpdateProfileRequest(userId, "Ana", "  Ana Maria da Silva  "));

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .DomainUsers.AsNoTracking().SingleAsync(item => item.Id == userId);

        user.FullName.Should().Be("Ana Maria da Silva", "o valor vem com trim do domínio");
    }

    [Fact]
    public async Task UpdateProfile_With_Empty_FullName_Should_Clear_It()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"profile-clear-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        await auth.UpdateProfileAsync(new UpdateProfileRequest(userId, "Ana", "Ana Maria da Silva"));
        var result = await auth.UpdateProfileAsync(new UpdateProfileRequest(userId, "Ana", ""));

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .DomainUsers.AsNoTracking().SingleAsync(item => item.Id == userId);

        user.FullName.Should().BeNull("string vazia é o pedido explícito de limpar");
    }

    [Fact]
    public async Task Login_Should_Carry_The_FullName_To_The_Session_Identity()
    {
        // O /auth/me lê CLAIM, não o banco — claim que não nascer no login
        // (AuthenticatedUser → CreatePrincipal) nunca chega à UI.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"profile-login-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await auth.UpdateProfileAsync(new UpdateProfileRequest(userId, "Ana", "Ana Maria da Silva"));

        var outcome = await auth.LoginAsync(new LoginRequest(email, StrongPassword));

        outcome.Failure.Should().BeNull();
        outcome.Authentication!.User.FullName.Should().Be("Ana Maria da Silva");
        outcome.Authentication.User.DisplayName.Should().Be("Ana");
    }

    // ── helpers (espelho do harness de AuthenticationSecurityAuditTests) ───

    private static async Task<Guid> SeedActiveUserAsync(IServiceScope scope, string email)
    {
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var register = await service.RegisterAsync(new RegisterUserRequest(email, "Profile User", StrongPassword));
        register.Succeeded.Should().BeTrue(string.Join("; ", register.Errors));

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.DomainUsers.SingleAsync(item => item.Email == email);
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
        services.AddOptions<SessionLifetimeOptions>();
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

        public Task TagAsync(string tag, string key, TimeSpan? duration, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
