using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Common.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse>(
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
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
        var resource = (request as IAuditableRequest)?.AuditResource ?? "ApplicationRequest";
        var actorId = currentUser.UserName;

        try
        {
            var response = await next();
            await WriteAsync(requestName, resource, "Succeeded", actorId, correlationId, null, cancellationToken);
            stopwatch.Stop();
            logger.LogInformation("Audit recorded for {RequestName} in {ElapsedMilliseconds} ms.", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            await WriteAsync(requestName, resource, "Failed", actorId, correlationId, exception.GetType().Name, cancellationToken);
            throw;
        }
    }

    private async Task WriteAsync(
        string requestName,
        string resource,
        string outcome,
        string? actorId,
        string? correlationId,
        string? error,
        CancellationToken cancellationToken)
    {
        // Apenas metadados são persistidos. Payloads podem conter senhas, tokens ou dados pessoais.
        await auditWriter.WriteAsync(
            new AuditEntry(requestName, resource, outcome, actorId: actorId, correlationId: correlationId, error: error),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
