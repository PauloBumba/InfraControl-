using System.Diagnostics;
using InfraControl.Application.AI;
using InfraControl.Contracts.AI;
using InfraControl.Infrastructure.AI;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.Workers.AI;

/// <summary>
/// Worker de inferência de IA que consome fila RabbitMQ e chama Ollama (ADR-044).
/// Permite escalar a capacidade de inferência com múltiplos workers.
/// Requests de IA vão para fila, workers processam assincronamente.
/// </summary>
public sealed class AiInferenceWorker(
    IAiProvider aiProvider,
    IOptions<AiOllamaOptions> options,
    ILogger<AiInferenceWorker> logger) : IConsumer<AiInferenceMessage>
{
    private readonly string _baseUrl = options.Value.BaseUrl.TrimEnd('/');

    public async Task Consume(ConsumeContext<AiInferenceMessage> context)
    {
        var message = context.Message;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "Processando inferência IA {Id} com modelo {Model}",
            message.Id,
            message.Model);

        try
        {
            // ADR-044: Usar Capability generica para inferência livre
            var request = new AiCompletionRequest(
                Capability: AiCapability.Copilot, // Capability genérica para inferência
                Prompt: message.Prompt,
                SystemPrompt: message.SystemPrompt,
                Generation: new AiGenerationOptions
                {
                    MaxTokens = message.MaxTokens,
                    Temperature = message.Temperature
                },
                Model: message.Model);

            var result = await aiProvider.CompleteAsync(request, context.CancellationToken);

            stopwatch.Stop();

            var response = new AiInferenceResponse
            {
                Id = message.Id,
                CorrelationId = message.CorrelationId,
                Response = result.Text,
                Success = true,
                TokensUsed = (result.PromptTokens ?? 0) + (result.CompletionTokens ?? 0),
                Duration = stopwatch.Elapsed
            };

            // Envia resposta para a fila de resposta
            await context.RespondAsync(response);

            logger.LogInformation(
                "Inferência IA {Id} concluída em {Duration}ms com {Tokens} tokens",
                message.Id,
                stopwatch.ElapsedMilliseconds,
                response.TokensUsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(ex, "Falha na inferência IA {Id}", message.Id);

            var response = new AiInferenceResponse
            {
                Id = message.Id,
                CorrelationId = message.CorrelationId,
                Response = string.Empty,
                Success = false,
                Error = ex.Message,
                TokensUsed = 0,
                Duration = stopwatch.Elapsed
            };

            await context.RespondAsync(response);
        }
    }
}
