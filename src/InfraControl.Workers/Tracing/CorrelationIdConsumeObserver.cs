using System.Runtime.CompilerServices;
using InfraControl.Infrastructure.Services;
using MassTransit;
// LogContext ambíguo com MassTransit.LogContext; usando o caminho completo abaixo

namespace InfraControl.Workers.Tracing;

/// <summary>
/// ADR-021: quando uma mensagem chega, o correlation id da requisição que a
/// originou está no header (colocado pelo MassTransitEventBus). Este observador
/// o levanta para o <see cref="LogContext"/> de TODA linha de log do
/// processamento da mensagem, sem os consumidores precisarem pedir no código.
///
/// O PreConsume e o PostConsume rodam dentro do MESMO fluxo async do pipeline
/// da mensagem no MassTransit, então o LogContext (AsyncLocal) flui como na
/// requisição HTTP. O disposable do push fica numa ConditionalWeakTable chaveada
/// pelo ConsumeContext: dispose no Post/ConsumeFault, e se algo no meio trocar
/// de contexto o valor é coletado em vez de vazar.
/// </summary>
public sealed class CorrelationIdConsumeObserver : IConsumeObserver
{
    private readonly ConditionalWeakTable<ConsumeContext, IDisposable> _pushed = new();

    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        var correlationId = context.Headers.TryGetHeader(MassTransitEventBus.CorrelationHeader, out var value)
            ? value?.ToString()
            : null;

        if (!string.IsNullOrWhiteSpace(correlationId) && !_pushed.TryGetValue(context, out _))
            _pushed.Add(context, Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId));

        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class => Pop(context);

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class => Pop(context);

    private Task Pop(ConsumeContext context)
    {
        if (_pushed.TryGetValue(context, out var disposable))
        {
            _pushed.Remove(context);
            disposable.Dispose();
        }

        return Task.CompletedTask;
    }
}