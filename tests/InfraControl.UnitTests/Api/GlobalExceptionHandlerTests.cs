using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Api.Middleware;
using InfraControl.Application.Common.Interfaces;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// F0-T023: o tratador global de exceções é o ponto único que embrulha falhas
/// em ApiResponse { isSuccess, data, errors } com o status HTTP correto e
/// mensagens prontas para a UX (pt-BR), sem vazar detalhes internos.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task KeyNotFound_ShouldReturn404_WithEnvelope()
    {
        var (status, json) = await HandleAsync(new KeyNotFoundException("Usuário '42' não encontrado."));

        status.Should().Be(StatusCodes.Status404NotFound);
        json.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        json.GetProperty("errors")[0].GetString().Should().Be("Usuário '42' não encontrado.");
    }

    [Fact]
    public async Task UnauthorizedAccess_WhenAnonymous_ShouldReturn401()
    {
        var (status, json) = await HandleAsync(
            new UnauthorizedAccessException("O token do convite é inválido."), authenticated: false);

        status.Should().Be(StatusCodes.Status401Unauthorized);
        json.GetProperty("errors")[0].GetString().Should().Be("O token do convite é inválido.");
    }

    [Fact]
    public async Task UnauthorizedAccess_WhenAuthenticated_ShouldReturn403()
    {
        var (status, _) = await HandleAsync(
            new UnauthorizedAccessException("Permissões ausentes: IdentityUsersManage"), authenticated: true);

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ArgumentException_ShouldReturn400_WithoutParameterSuffix()
    {
        var (status, json) = await HandleAsync(
            new ArgumentException("Um e-mail válido é obrigatório.", "email"));

        status.Should().Be(StatusCodes.Status400BadRequest);
        var message = json.GetProperty("errors")[0].GetString();
        message.Should().Be("Um e-mail válido é obrigatório.");
        message.Should().NotContain("(Parameter");
    }

    [Fact]
    public async Task InvalidOperationException_ShouldReturn409()
    {
        var (status, json) = await HandleAsync(
            new InvalidOperationException("O último administrador ativo não pode perder a função Admin."));

        status.Should().Be(StatusCodes.Status409Conflict);
        json.GetProperty("errors")[0].GetString()
            .Should().Be("O último administrador ativo não pode perder a função Admin.");
    }

    [Fact]
    public async Task RequestValidationException_ShouldReturn400_WithEachError_BeforeInvalidOperationRule()
    {
        var (status, json) = await HandleAsync(new RequestValidationException(
        [
            "A senha deve ter ao menos 8 caracteres.",
            "O e-mail é inválido."
        ]));

        status.Should().Be(StatusCodes.Status400BadRequest,
            "RequestValidationException herda de InvalidOperation mas é validação → 400");
        json.GetProperty("errors").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task DuplicateRequestException_ShouldReturn409()
    {
        var (status, json) = await HandleAsync(new DuplicateRequestException("abc-123"));

        status.Should().Be(StatusCodes.Status409Conflict);
        json.GetProperty("errors").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task UnknownException_ShouldReturn500_WithoutLeakingInternals()
    {
        var (status, json) = await HandleAsync(
            new Exception("Database connection failed at 10.0.0.5:5432 / senha=secret"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
        var errors = json.GetProperty("errors");
        errors.GetArrayLength().Should().Be(1);
        errors[0].GetString().Should().NotContain("secret").And.NotContain("10.0.0.5");
    }

    [Fact]
    public async Task HandledResponse_ShouldAlwaysHavePopulatedErrors()
    {
        var (_, json) = await HandleAsync(new InvalidOperationException("qualquer coisa"));

        json.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        json.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0,
            "toda falha precisa entregar mensagem exibível para a UX");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AntiforgeryValidation_ShouldReturn400_AndNotLeakWhetherTheTokenExisted(bool authenticated)
    {
        // F1-T015/ADR-017: 400 e não 403 — o usuário está autenticado, o que
        // falta é um header válido. E a mensagem é a mesma em qualquer caso:
        // dizer "token ausente" vs "token inválido" seria oráculo.
        var (status, json) = await HandleAsync(
            new AntiforgeryValidationException("The cookie token and the request token were swapped."),
            authenticated);

        status.Should().Be(StatusCodes.Status400BadRequest);
        json.GetProperty("isSuccess").GetBoolean().Should().BeFalse();

        var error = json.GetProperty("errors")[0].GetString();
        error.Should().Be("A requisição não pôde ser validada. Recarregue a página e tente novamente.");
        error.Should().NotContain("swapped");
    }

    private static async Task<(int Status, JsonElement Json)> HandleAsync(
        Exception exception,
        bool authenticated = false)
    {
        var context = new DefaultHttpContext();
        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "cookie")
            : new ClaimsIdentity();
        context.User = new ClaimsPrincipal(identity);

        await using var body = new MemoryStream();
        context.Response.Body = body;

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue("a exceção é responsabilidade do tratador global");
        context.Response.ContentType.Should().StartWith("application/json");

        body.Position = 0;
        var json = await JsonSerializer.DeserializeAsync<JsonElement>(body, JsonOptions);
        return (context.Response.StatusCode, json);
    }
}
