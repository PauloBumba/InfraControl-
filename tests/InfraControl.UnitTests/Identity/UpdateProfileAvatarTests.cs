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
/// F51 — campo <c>User.AvatarPath</c> (foto de perfil auto-editável).
///
/// Mesma armadilha do FullName (F38): o serviço só chama
/// <c>SetAvatarPath</c> quando o campo foi INFORMADO — ausente preserva,
/// <c>""</c> limpa, caminho válido grava. Caminho precisa ser
/// <c>/uploads/avatars/…</c>: URL externa ou outro módulo é recusado pelo
/// domínio (evita hotlink/SSRF de exibição).
/// </summary>
public sealed class UpdateProfileAvatarTests
{
    private const string StrongPassword = "ChangeMe!123456";

    [Fact]
    public async Task UpdateProfile_Without_AvatarPath_Should_Preserve_The_Stored_Value()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"avatar-keep-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        (await auth.UpdateProfileAsync(
            new UpdateProfileRequest(userId, "Ana", AvatarPath: "/uploads/avatars/foto1.png"))).Succeeded.Should().BeTrue();

        // Troca só o nome: AvatarPath ausente no contrato (null).
        var result = await auth.UpdateProfileAsync(new UpdateProfileRequest(userId, "Ana Nova"));

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .DomainUsers.AsNoTracking().SingleAsync(item => item.Id == userId);

        user.AvatarPath.Should().Be("/uploads/avatars/foto1.png",
            "trocar só o nome exibido não pode apagar a foto (ausente = preserva)");
    }

    [Fact]
    public async Task UpdateProfile_With_Valid_AvatarPath_Should_Store_It()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"avatar-set-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await auth.UpdateProfileAsync(
            new UpdateProfileRequest(userId, "Ana", AvatarPath: "/uploads/avatars/foto2.png"));

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .DomainUsers.AsNoTracking().SingleAsync(item => item.Id == userId);

        user.AvatarPath.Should().Be("/uploads/avatars/foto2.png");
    }

    [Fact]
    public async Task UpdateProfile_With_Empty_AvatarPath_Should_Clear_It()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"avatar-clear-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        await auth.UpdateProfileAsync(
            new UpdateProfileRequest(userId, "Ana", AvatarPath: "/uploads/avatars/foto3.png"));
        var result = await auth.UpdateProfileAsync(new UpdateProfileRequest(userId, "Ana", AvatarPath: ""));

        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors));
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .DomainUsers.AsNoTracking().SingleAsync(item => item.Id == userId);

        user.AvatarPath.Should().BeNull("string vazia é o pedido explícito de remover a foto");
    }

    [Theory]
    [InlineData("https://evil.example.com/x.png")]
    [InlineData("/uploads/knowledge/artigo.png")]
    [InlineData("/uploads/avatars/../segredo.png")]
    public async Task UpdateProfile_With_Foreign_AvatarPath_Should_Fail(string avatarPath)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"avatar-bad-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await auth.UpdateProfileAsync(
            new UpdateProfileRequest(userId, "Ana", AvatarPath: avatarPath));

        result.Succeeded.Should().BeFalse("só /uploads/avatars/… é aceito");
        result.Errors.Should().ContainSingle(error => error.Contains("Avatar inválido"));
    }

    [Fact]
    public async Task Login_Should_Carry_The_AvatarPath_To_The_Session_Identity()
    {
        // O /auth/me lê CLAIM, não o banco — a foto precisa nascer no
        // AuthenticatedUser para virar claim no CreatePrincipal.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var email = $"avatar-login-{Guid.NewGuid():N}@example.com";
        var userId = await SeedActiveUserAsync(scope, email);
        var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await auth.UpdateProfileAsync(
            new UpdateProfileRequest(userId, "Ana", AvatarPath: "/uploads/avatars/login.png"));

        var outcome = await auth.LoginAsync(new LoginRequest(email, StrongPassword));

        outcome.Failure.Should().BeNull();
        outcome.Authentication!.User.AvatarPath.Should().Be("/uploads/avatars/login.png");
    }

    // ── helpers (espelho do harness de UpdateProfileFullNameTests) ──────

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
