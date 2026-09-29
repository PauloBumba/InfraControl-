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
/// F1-T016a (ADR-019): enrollment de MFA TOTP — setup emite secret cifrado
/// (enabled=false), confirm valida um TOTP real e entrega 10 recovery codes uma
/// única vez, disable remove a linha (opt-out completo). Login sem código
/// continua funcionando até o confirm.
/// </summary>
public sealed class MfaEnrollmentTests
{
    private const string StrongPassword = "ChangeMe!123456";

    // Código inválido determinístico: com tolerância de ±1 período existe só
    // ~3 chances em 1e6 de colidir com uma janela válida — desprezível.
    private const string InvalidTotp = "000000";

    [Fact]
    public async Task Setup_ShouldReturnSecretAndKeepLoginWithoutCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-setup@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var setup = await service.SetupMfaAsync(userId);

        setup.Secret.Should().NotBeNullOrWhiteSpace("o setup devolve o secret p/ QR");
        setup.OtpauthUri.Should().StartWith("otpauth://totp/", "a UI (T016d) precisa da URI do QR");
        setup.OtpauthUri.Should().Contain(setup.Secret);

        var status = await service.GetMfaStatusAsync(userId);
        status.Configured.Should().BeTrue();
        status.Enabled.Should().BeFalse("setup sem confirm não pode exigir código no login");

        var login = await service.LoginAsync(new LoginRequest("mfa-setup@example.com", StrongPassword));
        login.Authentication.Should().NotBeNull(
            "enquanto o MFA não é confirmado o login por senha deve seguir inalterado");
        login.Failure.Should().BeNull();
    }

    [Fact]
    public async Task Setup_WhenAlreadyEnabled_ShouldThrowPtBrMessage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-enabled@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var setup = await service.SetupMfaAsync(userId);
        var confirm = await service.ConfirmMfaAsync(userId, ComputeTotp(setup.Secret));
        confirm.Succeeded.Should().BeTrue();

        var act = () => service.SetupMfaAsync(userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("MFA já está habilitado*",
                "reemitir secret sem desativar antes derrubaria a 2ª camada sem verificação");
    }

    [Fact]
    public async Task Confirm_WithValidTotp_ShouldEnableAndReturn10RecoveryCodes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-confirm@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var setup = await service.SetupMfaAsync(userId);
        var confirm = await service.ConfirmMfaAsync(userId, ComputeTotp(setup.Secret));

        confirm.Succeeded.Should().BeTrue(string.Join("; ", confirm.Errors));
        confirm.RecoveryCodes.Should().HaveCount(10);
        confirm.RecoveryCodes.Should().OnlyContain(code =>
            System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z2-7]{4}-[A-Z2-7]{4}$"),
            "recovery codes são Base32 no formato XXXX-XXXX");

        var status = await service.GetMfaStatusAsync(userId);
        status.Enabled.Should().BeTrue();
        status.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Confirm_WithInvalidCode_ShouldFailAndStayDisabled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-badcode@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        await service.SetupMfaAsync(userId);
        var confirm = await service.ConfirmMfaAsync(userId, InvalidTotp);

        confirm.Succeeded.Should().BeFalse();
        confirm.Errors.Should().Contain("Código MFA inválido.");
        confirm.RecoveryCodes.Should().BeEmpty("código errado não libera recovery codes");

        var status = await service.GetMfaStatusAsync(userId);
        status.Enabled.Should().BeFalse("confirm inválido não pode ligar o MFA");
    }

    [Fact]
    public async Task Confirm_WithoutSetup_ShouldFailPtBrMessage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-nosetup@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var confirm = await service.ConfirmMfaAsync(userId, "123456");

        confirm.Succeeded.Should().BeFalse();
        confirm.Errors.Should().Contain("MFA não está configurado para este usuário.");
    }

    [Fact]
    public async Task Status_BeforeSetup_ShouldReportNotConfigured()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-status@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var status = await service.GetMfaStatusAsync(userId);

        status.Configured.Should().BeFalse();
        status.Enabled.Should().BeFalse();
        status.ConfirmedAt.Should().BeNull();
    }

    [Fact]
    public async Task Disable_WithPassword_ShouldRemoveConfiguration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-disablepw@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var setup = await service.SetupMfaAsync(userId);
        (await service.ConfirmMfaAsync(userId, ComputeTotp(setup.Secret))).Succeeded.Should().BeTrue();

        var disable = await service.DisableMfaAsync(userId, code: null, password: StrongPassword);

        disable.Succeeded.Should().BeTrue(string.Join("; ", disable.Errors));
        var status = await service.GetMfaStatusAsync(userId);
        status.Configured.Should().BeFalse("opt-out apaga a linha, não só desliga");

        var login = await service.LoginAsync(new LoginRequest("mfa-disablepw@example.com", StrongPassword));
        login.Authentication.Should().NotBeNull("sem MFA volta ao login por senha puro");
        login.Failure.Should().BeNull();
    }

    [Fact]
    public async Task Disable_WithTotpCode_ShouldRemoveConfiguration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-disablecode@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var setup = await service.SetupMfaAsync(userId);
        (await service.ConfirmMfaAsync(userId, ComputeTotp(setup.Secret))).Succeeded.Should().BeTrue();

        var disable = await service.DisableMfaAsync(userId, code: ComputeTotp(setup.Secret), password: null);

        disable.Succeeded.Should().BeTrue(string.Join("; ", disable.Errors));
        (await service.GetMfaStatusAsync(userId)).Configured.Should().BeFalse();
    }

    [Fact]
    public async Task Disable_WithWrongCodeAndPassword_ShouldFailPtBrMessage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-disablebad@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var setup = await service.SetupMfaAsync(userId);
        (await service.ConfirmMfaAsync(userId, ComputeTotp(setup.Secret))).Succeeded.Should().BeTrue();

        var disable = await service.DisableMfaAsync(userId, code: InvalidTotp, password: "SenhaErrada!123");

        disable.Succeeded.Should().BeFalse();
        disable.Errors.Should().Contain("Código ou senha inválidos.");
        (await service.GetMfaStatusAsync(userId)).Configured.Should().BeTrue(
            "disable errado não pode remover a proteção");
    }

    [Fact]
    public async Task Disable_WithoutCodeAndPassword_ShouldFailPtBrMessage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var userId = await SeedActiveUserAsync(scope, "mfa-disablenone@example.com");
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        await service.SetupMfaAsync(userId);
        var disable = await service.DisableMfaAsync(userId, code: null, password: null);

        disable.Succeeded.Should().BeFalse();
        disable.Errors.Should().Contain("Informe o código MFA ou a senha.");
    }

    private static string ComputeTotp(string base32Secret)
        => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

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
