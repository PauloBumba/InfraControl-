using InfraControl.Application.AI.Validation;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Validation.Commands;

// F49-T001: ValidateConnection(provider, endpoint, apiKey) — dry-run
// Administer que devolve fases (conectividade ≠ autenticação ≠ modelos)
// sem tocar no cadastro. Key é write-only: passa pelo validator em
// memória e nunca é persistida, logada ou devolvida.
public sealed record ValidateAiConnectionCommand(
    AiProtocol Protocol,
    string BaseUrl,
    string? ApiKey,
    string? Model = null)
    : BaseCommand<AiConnectionValidationResult>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class ValidateAiConnectionCommandHandler(IAiConnectionValidator validator)
    : IRequestHandler<ValidateAiConnectionCommand, AiConnectionValidationResult>
{
    public async Task<AiConnectionValidationResult> Handle(
        ValidateAiConnectionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            throw new ArgumentException(
                "O endpoint (base URL) é obrigatório para validar a conexão.",
                nameof(request));

        var requiresKey = request.Protocol is AiProtocol.Gemini or AiProtocol.Anthropic;
        if (requiresKey && string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ArgumentException(
                $"A API key é obrigatória para o protocolo {request.Protocol}.",
                nameof(request));

        return await validator.ValidateAsync(
            new AiConnectionValidationRequest(
                request.Protocol,
                request.BaseUrl.Trim().TrimEnd('/'),
                request.ApiKey?.Trim(),
                request.Model?.Trim()),
            cancellationToken);
    }
}
