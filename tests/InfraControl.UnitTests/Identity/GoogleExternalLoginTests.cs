using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Api.Controllers.Identity;
using InfraControl.Api.ViewModels.Identity;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Authentication;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F1-T014 — login social Google. Regras de segurança cobertas aqui:
/// a credencial do browser NUNCA é fonte de verdade (a porta valida), conta
/// inexistente não é provisionada, conta inativa não entra (F1-T013) e as
/// falhas de "conta" são indistinguíveis entre si (anti-enumeração).
/// </summary>
public sealed class GoogleExternalLoginTests
{
    private const string StrongPassword = "ChangeMe!123456";
    private const string IdToken = "header.payload.signature";

    private static readonly GoogleIdentity GoogleUser =
        new("google-subject-123", "google-user@example.com", "Google User");

    [Fact]
    public async Task LoginWithGoogle_ShouldFail_WhenProviderNotConfigured()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection, GoogleTokenValidation.NotConfigured());
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await service.LoginWithGoogleAsync(new GoogleLoginRequest(IdToken));

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(ExternalLoginFailure.ProviderNotConfigured);
    }

    [Fact]
    public async Task LoginWithGoogle_ShouldFail_WhenTokenIsInvalid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection, GoogleTokenValidation.Invalid());
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await service.LoginWithGoogleAsync(new GoogleLoginRequest("token.forjado.assinatura"));

        result.Succeeded.Should().BeFalse("an unverified token must never open a session");
        result.Failure.Should().Be(ExternalLoginFailure.InvalidCredential);
    }

    [Fact]
    public async Task LoginWithGoogle_ShouldRejectUnknownAccount_WithoutCreatingUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection, GoogleTokenValidation.Valid(GoogleUser));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();

        var result = await service.LoginWithGoogleAsync(new GoogleLoginRequest(IdToken));

        result.Succeeded.Should().BeFalse("social login must not auto-provision accounts");
        result.Failure.Should().Be(ExternalLoginFailure.AccountUnavailable);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.DomainUsers.CountAsync()).Should().Be(0,
            "a first Google sign-in must not create a domain user");
    }

    [Fact]
    public async Task LoginWithGoogle_ShouldRejectInactiveAccount_WithSameFailureAsUnknown()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection, GoogleTokenValidation.Valid(GoogleUser));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(new RegisterUserRequest(GoogleUser.Email, "Google User", StrongPassword));

        var result = await service.LoginWithGoogleAsync(new GoogleLoginRequest(IdToken));

        result.Succeeded.Should().BeFalse("an inactive account must not log in with Google");
        result.Failure.Should().Be(ExternalLoginFailure.AccountUnavailable,
            "the failure must be identical to the unknown-account case (anti-enumeration)");
    }

    [Fact]
    public async Task LoginWithGoogle_ShouldStartSessionForActiveAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var validator = new StubGoogleTokenValidator(GoogleTokenValidation.Valid(GoogleUser));
        await using var provider = BuildProvider(connection, validator);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(new RegisterUserRequest(GoogleUser.Email, "Google User", StrongPassword));
        await SetActiveAsync(scope, GoogleUser.Email);

        var result = await service.LoginWithGoogleAsync(new GoogleLoginRequest(IdToken));

        result.Succeeded.Should().BeTrue();
        result.Result!.User.Email.Should().Be(GoogleUser.Email);
        result.Result.User.Roles.Should().ContainSingle().Which.Should().Be("Viewer");

        validator.ReceivedTokens.Should().ContainSingle().Which.Should().Be(IdToken,
            "the raw id_token must reach the validator unchanged");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await dbContext.UserSessions.SingleAsync();
        session.UserId.Should().NotBe(Guid.Empty);
        session.RefreshTokenHash.Should().NotBe(result.Result.RefreshToken,
            "the refresh token is returned once and stored only as hash");
    }

    [Fact]
    public async Task LoginWithGoogle_ShouldUseNormalizedEmail_RegardlessOfCase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var mixedCase = GoogleUser with { Email = "Mixed.Case@Example.com" };
        await using var provider = BuildProvider(connection, GoogleTokenValidation.Valid(mixedCase));
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>();
        await service.RegisterAsync(new RegisterUserRequest("mixed.case@example.com", "Mixed", StrongPassword));
        await SetActiveAsync(scope, "mixed.case@example.com");

        var result = await service.LoginWithGoogleAsync(new GoogleLoginRequest(IdToken));

        result.Succeeded.Should().BeTrue("the Google email is normalized like the password login");
    }

    [Fact]
    public async Task Controller_ShouldTranslateFailures_WithoutRevealingAccount()
    {
        (ExternalLoginFailure Failure, int Status)[] cases =
        [
            (ExternalLoginFailure.ProviderNotConfigured, StatusCodes.Status503ServiceUnavailable),
            (ExternalLoginFailure.InvalidCredential, StatusCodes.Status401Unauthorized),
            (ExternalLoginFailure.AccountUnavailable, StatusCodes.Status403Forbidden)
        ];

        foreach (var (failure, expectedStatus) in cases)
        {
            var service = new Mock<IIdentityAuthenticationService>();
            service.Setup(item => item.LoginWithGoogleAsync(It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ExternalLoginResult.Failed(failure));

            var (status, json) = await CallControllerAsync(service.Object, GoogleUser.Email);

            status.Should().Be(expectedStatus);
            json.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
            var message = json.GetProperty("errors")[0].GetString()!;
            message.Should().NotContain(GoogleUser.Email,
                "the response must never echo the e-mail used in the attempt");
        }
    }

    [Fact]
    public async Task Controller_ShouldReturnSameMessage_ForUnknownAndInactiveAccount()
    {
        var (unknownStatus, unknownJson) = await CallControllerAsync(
            MockService(ExternalLoginResult.Failed(ExternalLoginFailure.AccountUnavailable)), "novo@example.com");
        var (inactiveStatus, inactiveJson) = await CallControllerAsync(
            MockService(ExternalLoginResult.Failed(ExternalLoginFailure.AccountUnavailable)), "inativo@example.com");

        unknownStatus.Should().Be(inactiveStatus);
        unknownJson.GetProperty("errors")[0].GetString()
            .Should().Be(inactiveJson.GetProperty("errors")[0].GetString(),
                "unknown and inactive accounts must be indistinguishable to the caller");
    }

    [Fact]
    public async Task Controller_ShouldSignInCookie_AndReturnEnvelope()
    {
        var session = new AuthenticationResult(
            new AuthenticatedUser(Guid.NewGuid(), GoogleUser.Email, "Google User", [nameof(Domain.Enums.UserRole.Viewer)]),
            "refresh-token",
            DateTime.UtcNow.AddDays(30));

        var service = MockService(ExternalLoginResult.Success(session));
        var (status, json) = await CallControllerAsync(service, GoogleUser.Email);

        status.Should().Be(StatusCodes.Status200OK);
        json.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        json.GetProperty("data").GetProperty("user").GetProperty("email").GetString().Should().Be(GoogleUser.Email);
    }

    [Fact]
    public void GoogleAuthOptions_ShouldTreatMissingConfigurationAsDisabled()
    {
        new GoogleAuthOptions { ClientId = "id", ClientSecret = "" }.IsConfigured.Should().BeFalse();
        new GoogleAuthOptions { ClientId = "", ClientSecret = "secret" }.IsConfigured.Should().BeFalse();
        new GoogleAuthOptions { ClientId = "id", ClientSecret = "secret" }.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData("user@empresa.com.br", "[]", true)]
    [InlineData("allowed@empresa.com.br", "[\"empresa.com.br\"]", true)]
    [InlineData("mixed.case@empresa.com.br", "[\"EMPRESA.com.BR\"]", true)]
    [InlineData("user@empresa.com.br", "[\" @empresa.com.br \"]", true)]
    [InlineData("user@outra.com", "[\"empresa.com.br\"]", false)]
    [InlineData("user@sub.empresa.com.br", "[\"empresa.com.br\"]", false)]
    // Variável de ambiente sobrando como AllowedDomains__0= não pode trancar o login.
    [InlineData("user@empresa.com.br", "[\"\", \"   \"]", true)]
    [InlineData("email-sem-arroba", "[\"empresa.com.br\"]", false)]
    public void GoogleAuthOptions_ShouldApplyDomainAllowList(string email, string domainsJson, bool expected)
    {
        var options = new GoogleAuthOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            AllowedDomains = JsonSerializer.Deserialize<string[]>(domainsJson)!,
        };

        options.IsDomainAllowed(email).Should().Be(expected);
    }

    [Fact]
    public void ViewModel_ShouldNotAcceptOversizedCredential()
    {
        var viewModel = new GoogleLoginViewModel(new string('a', 9000));
        var results = new List<ValidationResult>();
        var context = new ValidationContext(viewModel);

        Validator.TryValidateObject(viewModel, context, results, true);

        results.Should().NotBeEmpty("the id_token comes from an untrusted client");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IIdentityAuthenticationService MockService(ExternalLoginResult result)
    {
        var service = new Mock<IIdentityAuthenticationService>();
        service.Setup(item => item.LoginWithGoogleAsync(It.IsAny<GoogleLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return service.Object;
    }

    private static async Task<(int Status, JsonElement Json)> CallControllerAsync(
        IIdentityAuthenticationService service,
        string email)
    {
        var authServices = new ServiceCollection()
            .AddLogging()
            .AddAuthentication(options => options.DefaultScheme = IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme)
            .Services
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = authServices };
        var controller = new AuthenticationController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var action = await controller.GoogleLogin(new GoogleLoginViewModel(IdToken), CancellationToken.None);

        var status = action switch
        {
            ObjectResult objectResult => objectResult.StatusCode!.Value,
            StatusCodeResult statusCode => statusCode.StatusCode,
            _ => throw new InvalidOperationException("O endpoint de login Google deve responder com status explícito.")
        };

        var payload = action switch
        {
            ObjectResult objectResult => objectResult.Value,
            _ => null
        };

        var json = JsonSerializer.SerializeToElement(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return (status, json);
    }

    private static async Task SetActiveAsync(IServiceScope scope, string email)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.DomainUsers.SingleAsync(item => item.Email == email);
        user.Enable();
        await dbContext.SaveChangesAsync();
    }

    private static ServiceProvider BuildProvider(
        SqliteConnection connection,
        GoogleTokenValidation validation)
        => BuildProvider(connection, new StubGoogleTokenValidator(validation));

    private static ServiceProvider BuildProvider(
        SqliteConnection connection,
        IGoogleIdentityTokenValidator validator)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<InfraControlUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<ICacheService, NoOpCacheService>();
        services.AddScoped<IIdentityEmailSender, NoOpEmailSender>();
        services.AddSingleton(validator);
        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
        return services.BuildServiceProvider();
    }

    private sealed class StubGoogleTokenValidator(GoogleTokenValidation validation) : IGoogleIdentityTokenValidator
    {
        public List<string> ReceivedTokens { get; } = [];

        public Task<GoogleTokenValidation> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
        {
            ReceivedTokens.Add(idToken);
            return Task.FromResult(validation);
        }
    }

    private sealed class NoOpEmailSender : IIdentityEmailSender
    {
        public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
