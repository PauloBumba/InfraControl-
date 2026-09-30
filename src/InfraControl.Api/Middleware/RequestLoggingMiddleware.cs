using System.Diagnostics;
using Serilog.Context;

namespace InfraControl.Api.Middleware;

/// <summary>
/// ADR-021: o <c>CorrelationId</c> de toda requisição e o evento de log que
/// representa a requisição.
///
/// É o PRIMEIRO middleware do pipeline, antes de tudo, inclusive do Serilog
/// request logging: se ele rodar depois, a requisição que estourou não tem
/// correlation id na linha.
///
/// Três responsabilidades, todas de uma vez porque as três dependem do mesmo id:
/// 1. escolhe o id (respeita o header do cliente, senão usa o do servidor);
/// 2. publica no <see cref="LogContext"/>, para toda linha de log carregá-lo
///    sem ninguém pedir no código;
/// 3. devolve o id no header de resposta, para o suporte conseguir citá-lo.
///
/// O evento final carrega o TEMPLATE da rota, não o path literal: com path
/// literal cada id cria uma linha nova e a plataforma perde a única coisa que
/// faz — agregar. E nunca carrega corpo nem query string, que é onde moram
/// senha, refresh token, código de MFA e base URL de convite.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string CorrelationIdProperty = "CorrelationId";

    /// <summary>Log de chamada abaixo disso é ruído de framework (health check, probe).</summary>
    private static readonly PathString[] QuietPaths = ["/health", "/health/ready", "/health/live", "/health/startup", "/favicon.ico"];

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationIdProperty] = correlationId;

        // o header de resposta vai no onstarting: se a resposta já começou a
        // sair, não dá mais para mudar cabeçalho e a escrita estoura
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationIdProperty, correlationId))
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await next(context);
            }
            finally
            {
                // no finally: uma exceção não pode custar o log da requisição
                // que a causou — é justamente a linha que se procura depois
                var elapsed = Stopwatch.GetElapsedTime(started);
                Write(context, correlationId, elapsed);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        // respeitar o id do cliente é o que permite seguir um rastro que o
        // suporte colou da tela; e o TraceIdentifier evita um id vazio
        var incoming = context.Request.Headers[CorrelationIdHeader].ToString();
        return string.IsNullOrWhiteSpace(incoming) ? context.TraceIdentifier : incoming.Trim();
    }

    private void Write(HttpContext context, string correlationId, TimeSpan elapsed)
    {
        if (QuietPaths.Contains(context.Request.Path))
            return;

        var status = context.Response.StatusCode;
        var method = context.Request.Method;

        logger.Log(
            LevelFor(status),
            "Requisição {Method} {Route} respondeu {StatusCode} em {ElapsedMilliseconds}ms para {UserId}",
            method,
            RouteTemplate(context),
            status,
            (long)elapsed.TotalMilliseconds,
            context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);

        static LogLevel LevelFor(int status) => status switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }

    /// <summary>
    /// O template da rota ("/api/v1/devices/{id}"). Antes do roteamento
    ///matchar não existe endpoint, e aí cai para o path — que é o melhor que
    /// se pode fazer, não o ideal.
    /// </summary>
    private static string RouteTemplate(HttpContext context)
        => context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.RouteEndpoint>()?.RoutePattern.RawText
           ?? context.Request.Path.Value
           ?? "/";
}
