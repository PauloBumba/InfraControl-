using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Validation;
using InfraControl.Application.AI.Validation.Commands;
using InfraControl.Domain.Enums;
using InfraControl.BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// F49-T001: ValidateConnection(provider, endpoint, apiKey) — dry-run
/// Administer que devolve fases (conectividade/autenticação/modelos) sem
/// tocar no cadastro; key é write-only (só em memória do validator).
/// </summary>
public sealed class ValidateAiConnectionCommandTests
{
    [Fact]
    public void Command_RequiresAdministerRole()
    {
        var command = new ValidateAiConnectionCommand(
            AiProtocol.OpenAiCompatible, "https://api.example.com/v1", "sk-test");

        command.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Administer);
    }

    [Fact]
    public async Task Handle_PassesRequestToValidator_ReturnsFases()
    {
        var expected = new AiConnectionValidationResult(
            Ok: true,
            Connectivity: AiConnectionPhase.Success("conectividade"),
            Authentication: AiConnectionPhase.Success("autenticação"),
            Models: AiConnectionPhase.Success("modelos"),
            ModelsFound: ["model-a"],
            DetectedCapabilities: [],
            ModelCheck: null);
        var validator = new StubAiConnectionValidator(expected);
        var handler = new ValidateAiConnectionCommandHandler(validator);

        var result = await handler.Handle(new ValidateAiConnectionCommand(
            AiProtocol.Gemini,
            "https://generativelanguage.googleapis.com/v1beta",
            "AIza-secret",
            Model: "gemini-2.5-flash"), CancellationToken.None);

        result.Should().BeSameAs(expected);
        validator.LastRequest.Should().Be(new AiConnectionValidationRequest(
            AiProtocol.Gemini,
            "https://generativelanguage.googleapis.com/v1beta",
            "AIza-secret",
            "gemini-2.5-flash"));
    }

    [Fact]
    public async Task Handle_EmptyBaseUrl_Throws()
    {
        var handler = new ValidateAiConnectionCommandHandler(new StubAiConnectionValidator(
            new AiConnectionValidationResult(
                false, AiConnectionPhase.Skipped("c"), AiConnectionPhase.Skipped("a"),
                AiConnectionPhase.Skipped("m"), [], [], null)));

        var act = async () => await handler.Handle(new ValidateAiConnectionCommand(
            AiProtocol.OpenAiCompatible, "  ", "sk-test"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*endpoint*");
    }

    [Fact]
    public async Task Handle_OllamaWithoutKey_IsAllowed()
    {
        var validator = new StubAiConnectionValidator(new AiConnectionValidationResult(
            true,
            AiConnectionPhase.Success("conectividade"),
            AiConnectionPhase.Success("autenticação"),
            AiConnectionPhase.Success("modelos"),
            ["qwen2.5-coder:1.5b"],
            [],
            null));
        var handler = new ValidateAiConnectionCommandHandler(validator);

        var result = await handler.Handle(new ValidateAiConnectionCommand(
            AiProtocol.Ollama, "http://ollama-lb:80", ApiKey: null), CancellationToken.None);

        result.Ok.Should().BeTrue();
        validator.LastRequest!.ApiKey.Should().BeNull();
    }

    [Fact]
    public void DiRegistration_ResolvesValidator()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAiConnectionValidator>(_ => new AiConnectionValidator());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAiConnectionValidator>()
            .Should().BeOfType<AiConnectionValidator>();
    }
}

internal sealed class StubAiConnectionValidator(AiConnectionValidationResult result)
    : IAiConnectionValidator
{
    public AiConnectionValidationRequest? LastRequest { get; private set; }

    public Task<AiConnectionValidationResult> ValidateAsync(
        AiConnectionValidationRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(result);
    }
}
