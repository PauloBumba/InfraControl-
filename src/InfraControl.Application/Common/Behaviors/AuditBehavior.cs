using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse>(
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork,
    ILogger<AuditBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestName = typeof(TRequest).Name;
        var correlationId = Activity.Current?.TraceId.ToString();

        try
        {
            var response = await next();
            await WriteAsync(requestName, "Succeeded", correlationId, null, cancellationToken);
            stopwatch.Stop();
            logger.LogInformation("Audit recorded for {RequestName} in {ElapsedMilliseconds} ms.", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            await WriteAsync(requestName, "Failed", correlationId, exception.GetType().Name, cancellationToken);
            throw;
        }
    }

    private async Task WriteAsync(
        string requestName,
        string outcome,
        string? correlationId,
        string? error,
        CancellationToken cancellationToken)
    {
        // Apenas metadados são persistidos. Payloads podem conter senhas, tokens ou dados pessoais.
        await auditWriter.WriteAsync(
            new AuditEntry(requestName, "ApplicationRequest", outcome, correlationId: correlationId, error: error),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
