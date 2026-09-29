using System.Security.Cryptography;
using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Authentication;
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

public sealed class AuthenticationActivationTests
{
    private const string StrongPassword = "ChangeMe!123456";

    [Fact]
    public async Task Register_ShouldCreateInactiveUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var result = await service.RegisterAsync(
            new RegisterUserRequest("new-user@example.com", "New User", StrongPassword));

        result.Succeeded.Should().BeTrue();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainUser = await dbContext.DomainUsers
            .SingleAsync(user => user.Email == "new-user@example.com");

        domainUser.IsActive.Should().BeFalse(
            "self-registration must not activate the account; activation happens via invitation or admin enable");
    }

    [Fact]
    public async Task Login_ShouldRejectInactiveUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(
            new RegisterUserRequest("inactive-login@example.com", "Inactive", StrongPassword));

        var login = await service.LoginAsync(
            new LoginRequest("inactive-login@example.com", StrongPassword));

        login.Authentication.Should().BeNull("an inactive account must not be able to log in");
        login.Failure.Should().Be(LoginFailure.InvalidCredentials);
    }

    [Fact]
    public async Task Login_ShouldAllowActiveUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(
            new RegisterUserRequest("active-login@example.com", "Active", StrongPassword));
        await SetActiveAsync(scope, "active-login@example.com", active: true);

        var login = await service.LoginAsync(
            new LoginRequest("active-login@example.com", StrongPassword));

        login.Failure.Should().BeNull();
        login.Authentication.Should().NotBeNull();
    }

    [Fact]
    public async Task ForgotPassword_ShouldNotSendEmailForInactiveUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(
            new RegisterUserRequest("inactive-reset@example.com", "Inactive Reset", StrongPassword));

        var result = await service.RequestPasswordResetAsync(
            new ForgotPasswordRequest("inactive-reset@example.com", "https://localhost"));

        var sender = (FakeEmailSender)scope.ServiceProvider.GetRequiredService<IIdentityEmailSender>();
        sender.Sent.Should().BeEmpty("an inactive account must not receive password recovery");
        result.Message.Should().Be(
            "Se o e-mail estiver cadastrado, as instruções de redefinição de senha serão enviadas.",
            "the response must not reveal whether the account is inactive");
    }

    [Fact]
    public async Task ForgotPassword_ShouldSendEmailForActiveUserKeepingSameMessage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(
            new RegisterUserRequest("active-reset@example.com", "Active Reset", StrongPassword));
        await SetActiveAsync(scope, "active-reset@example.com", active: true);

        var result = await service.RequestPasswordResetAsync(
            new ForgotPasswordRequest("active-reset@example.com", "https://localhost"));

        var sender = (FakeEmailSender)scope.ServiceProvider.GetRequiredService<IIdentityEmailSender>();
        sender.Sent.Should().ContainSingle();
        result.Message.Should().Be(
            "Se o e-mail estiver cadastrado, as instruções de redefinição de senha serão enviadas.");
    }

    [Fact]
    public async Task ForgotPassword_ShouldNotRevealUnknownEmail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        var result = await service.RequestPasswordResetAsync(
            new ForgotPasswordRequest("unknown@example.com", "https://localhost"));

        var sender = (FakeEmailSender)scope.ServiceProvider.GetRequiredService<IIdentityEmailSender>();
        sender.Sent.Should().BeEmpty();
        result.Message.Should().Be(
            "Se o e-mail estiver cadastrado, as instruções de redefinição de senha serão enviadas.");
    }

    [Fact]
    public async Task ResetPassword_ShouldFailForUserDisabledAfterRequest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(
            new RegisterUserRequest("disabled-reset@example.com", "Disabled Reset", StrongPassword));
        await SetActiveAsync(scope, "disabled-reset@example.com", active: true);

        await service.RequestPasswordResetAsync(
            new ForgotPasswordRequest("disabled-reset@example.com", "https://localhost"));

        var sender = (FakeEmailSender)scope.ServiceProvider.GetRequiredService<IIdentityEmailSender>();
        var token = ExtractToken(sender.Sent.Single().ResetUrl);

        await SetActiveAsync(scope, "disabled-reset@example.com", active: false);

        var result = await service.ResetPasswordAsync(
            new ResetPasswordRequest("disabled-reset@example.com", token, "NewPass!123456"));

        result.Succeeded.Should().BeFalse(
            "an account disabled after the request must not be able to reset the password");
        result.Errors.Should().Contain("Solicitação de redefinição de senha inválida.");
    }

    [Fact]
    public async Task ResetPassword_ShouldSucceedForActiveUserAndAllowLogin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(
            new RegisterUserRequest("valid-reset@example.com", "Valid Reset", StrongPassword));
        await SetActiveAsync(scope, "valid-reset@example.com", active: true);

        await service.RequestPasswordResetAsync(
            new ForgotPasswordRequest("valid-reset@example.com", "https://localhost"));

        var sender = (FakeEmailSender)scope.ServiceProvider.GetRequiredService<IIdentityEmailSender>();
        var token = ExtractToken(sender.Sent.Single().ResetUrl);

        var result = await service.ResetPasswordAsync(
            new ResetPasswordRequest("valid-reset@example.com", token, "NewPass!123456"));

        result.Succeeded.Should().BeTrue();

        var login = await service.LoginAsync(
            new LoginRequest("valid-reset@example.com", "NewPass!123456"));
        login.Authentication.Should().NotBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task SetActiveAsync(IServiceScope scope, string email, bool active)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.DomainUsers.SingleAsync(item => item.Email == email);
        if (active) user.Enable(); else user.Disable();
        await dbContext.SaveChangesAsync();
    }

    private static string ExtractToken(string resetUrl)
    {
        var marker = "token=";
        var start = resetUrl.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "the reset url must contain a token");
        var token = resetUrl[(start + marker.Length)..];
        var ampersand = token.IndexOf('&');
        if (ampersand >= 0) token = token[..ampersand];
        return Uri.UnescapeDataString(token);
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
        // F1-T016a: o serviço de login passou a depender do protector (MFA).
        services.Configure<CredentialEncryptionOptions>(options =>
            options.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        services.AddScoped<ICredentialProtector, AesGcmCredentialProtector>();
        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
        return services.BuildServiceProvider();
    }

    // F1-T014: o login social não é objeto deste arquivo; o stub mantém o
    // grafo de DI completo e devolve "não configurado".
    private sealed class NotConfiguredGoogleValidator : IGoogleIdentityTokenValidator
    {
        public Task<GoogleTokenValidation> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
            => Task.FromResult(GoogleTokenValidation.NotConfigured());
    }

    private sealed class FakeEmailSender : IIdentityEmailSender
    {
        public List<(string Email, string ResetUrl)> Sent { get; } = [];

        public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
        {
            Sent.Add((email, resetUrl));
            return Task.CompletedTask;
        }
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
