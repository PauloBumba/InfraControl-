using System.Net;
using System.Text;
using FluentAssertions;
using InfraControl.Application.AI.Validation;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// F49-T001: validação de conexão em fases — conectividade, autenticação e
/// descoberta de modelos são estados DISTINTOS (um HTTP 200 no caminhão
/// não prova que a key funciona; 401 não é "sem rede"). Dry-run: aceita
/// endpoint+apiKey arbitrários sem tocar no cadastro.
/// </summary>
public sealed class AiConnectionValidatorTests
{
    private static HttpResponseMessage Json(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    [Fact]
    public async Task Validate_ReachableButUnauthorized_ReportsAuthFailure()
    {
        var handler = new StubHttpMessageHandler((request) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/models"))
                return Json(401, """{"error":{"message":"Incorrect API key provided."}}""");
            return Json(200, "{}");
        });
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.OpenAiCompatible,
            "https://api.example.com/v1",
            "sk-invalid"));

        result.Connectivity.Status.Should().Be(AiPhaseStatus.Success);
        result.Authentication.Status.Should().Be(AiPhaseStatus.Failed);
        result.Authentication.Error.Should().Contain("API key");
        result.Models.Status.Should().Be(AiPhaseStatus.Skipped);
        result.ModelsFound.Should().BeEmpty();
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ValidKey_ReturnsDiscoveredModels()
    {
        var handler = new StubHttpMessageHandler((request) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/models"))
                return Json(200,
                    """{"data":[{"id":"gpt-4o-mini"},{"id":"gpt-4o"}]}""");
            return Json(200, "{}");
        });
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.OpenAiCompatible,
            "https://api.example.com/v1",
            "sk-valid"));

        result.Ok.Should().BeTrue();
        result.Authentication.Status.Should().Be(AiPhaseStatus.Success);
        result.Models.Status.Should().Be(AiPhaseStatus.Success);
        result.ModelsFound.Should().BeEquivalentTo(new[] { "gpt-4o-mini", "gpt-4o" });
    }

    [Fact]
    public async Task Validate_UnreachableEndpoint_ReportsConnectivityFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("No such host is known."));
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.OpenAiCompatible,
            "https://no-such-host.invalid/v1",
            "sk-anything"));

        result.Connectivity.Status.Should().Be(AiPhaseStatus.Failed);
        result.Connectivity.Error.Should().Contain("endereço");
        result.Authentication.Status.Should().Be(AiPhaseStatus.Skipped);
        result.Models.Status.Should().Be(AiPhaseStatus.Skipped);
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_Timeout_ReportsConnectivityFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new TaskCanceledException());
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.OpenAiCompatible,
            "https://slow.example.com/v1",
            "sk-anything"));

        result.Connectivity.Status.Should().Be(AiPhaseStatus.Failed);
        result.Connectivity.Error.Should().Contain("tempo");
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_GeminiProtocol_UsesKeyQueryAndStripsModelsPrefix()
    {
        Uri? seenUri = null;
        string? seenKey = null;
        var handler = new StubHttpMessageHandler((request) =>
        {
            seenUri = request.RequestUri;
            seenKey = request.RequestUri!.Query.Contains("key=")
                ? "query" : "other";
            return Json(200,
                """{"models":[{"name":"models/gemini-2.5-flash"},{"name":"models/gemini-2.5-pro"}]}""");
        });
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.Gemini,
            "https://generativelanguage.googleapis.com/v1beta",
            "AIza-secret"));

        seenUri!.AbsolutePath.Should().Be("/v1beta/models");
        seenUri.Query.Should().Contain("key=AIza-secret");
        seenKey.Should().Be("query");
        result.ModelsFound.Should().BeEquivalentTo(
            new[] { "gemini-2.5-flash", "gemini-2.5-pro" });
    }

    [Fact]
    public async Task Validate_AnthropicProtocol_UsesXApiKeyHeader()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHttpMessageHandler((request) =>
        {
            seen = request;
            return Json(200, """{"data":[{"id":"claude-sonnet-4-5"}]}""");
        });
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.Anthropic,
            "https://api.anthropic.com",
            "sk-ant-secret"));

        seen!.RequestUri!.AbsolutePath.Should().Be("/v1/models");
        seen.Headers.GetValues("x-api-key").Should().ContainSingle("sk-ant-secret");
        seen.Headers.GetValues("anthropic-version").Should().ContainSingle("2023-06-01");
        result.ModelsFound.Should().BeEquivalentTo(new[] { "claude-sonnet-4-5" });
    }

    [Fact]
    public async Task Validate_Forbidden_ReportsPermissionFailure_NotAuth()
    {
        var handler = new StubHttpMessageHandler((request) =>
            request.RequestUri!.AbsolutePath.EndsWith("/models")
                ? Json(403, """{"error":{"message":"You do not have access."}}""")
                : Json(200, "{}"));
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.OpenAiCompatible,
            "https://api.example.com/v1",
            "sk-ok-but-forbidden"));

        result.Authentication.Status.Should().Be(AiPhaseStatus.Failed);
        result.Authentication.Error.Should().Contain("permissão");
    }

    [Fact]
    public async Task Validate_ServerError_ReportsCompatibilityFailure()
    {
        var handler = new StubHttpMessageHandler((request) =>
            request.RequestUri!.AbsolutePath.EndsWith("/models")
                ? Json(500, """{"error":"internal"}""")
                : Json(200, "{}"));
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.OpenAiCompatible,
            "https://api.example.com/v1",
            "sk-valid"));

        result.Models.Status.Should().Be(AiPhaseStatus.Failed);
        result.Models.Error.Should().Contain("500");
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_OllamaProtocol_ProbesTagsEndpoint()
    {
        Uri? seen = null;
        var handler = new StubHttpMessageHandler((request) =>
        {
            seen = request.RequestUri;
            return Json(200, """{"models":[{"name":"qwen2.5-coder:1.5b"}]}""");
        });
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.Ollama,
            "http://ollama-lb:80",
            ApiKey: null));

        seen!.AbsolutePath.Should().Be("/api/tags");
        result.Ok.Should().BeTrue();
        result.ModelsFound.Should().BeEquivalentTo(new[] { "qwen2.5-coder:1.5b" });
    }

    [Fact]
    public async Task Validate_ModelRequestedButNotInList_ReportsModelUnavailable()
    {
        var handler = new StubHttpMessageHandler((request) =>
            request.RequestUri!.AbsolutePath.EndsWith("/models")
                ? Json(200, """{"data":[{"id":"other-model"}]}""")
                : Json(200, "{}"));
        var validator = new AiConnectionValidator(handler);

        var result = await validator.ValidateAsync(new AiConnectionValidationRequest(
            AiProtocol.OpenAiCompatible,
            "https://api.example.com/v1",
            "sk-valid",
            Model: "gpt-4o"));

        result.Ok.Should().BeFalse();
        result.ModelCheck.Should().NotBeNull();
        result.ModelCheck!.Status.Should().Be(AiPhaseStatus.Failed);
        result.ModelCheck.Error.Should().Contain("gpt-4o");
    }
}
