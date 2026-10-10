using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.AI.Tools;

/// <summary>
/// Executor de tools de IA (FAI-T008/S7 · docs/34 §41/§52 · ADR-040):
/// o caminho <c>Tool → Policy → Permission</c> do LEVEL 0.
///
/// - autorização = a MESMA hierarquia do pipeline MediatR
///   (<see cref="IAuthorizedRequest"/>/ADR-016): ator autenticado + overlap
///   de roles exigidas pela tool — um Viewer não usa IA para ler o que
///   não leria direto (§41);
/// - timeout por tool (§52) com <c>AiToolTimeoutException</c>;
/// - UMA entrada de auditoria <c>AI.Tool.{nome}</c> por execução —
///   Succeeded/Denied/Failed, ator, trace (§41/§43) — no mecanismo de
///   auditoria de domínio existente (F0-T014);
/// - falha/timeout/denegação viram exceção pt-BR com causa preservada.
/// </summary>
public sealed class AiToolExecutor(
    IAiToolRegistry registry,
    ICurrentUserContext currentUser,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork,
    ILogger<AiToolExecutor> logger) : IAiToolExecutor
{
    public async Task<AiToolResult> ExecuteAsync(
        string toolName,
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var tool = registry.Get(toolName);
        var descriptor = tool.Descriptor;

        // §41: mesma regra do AuthorizationBehavior — autenticação é
        // sempre exigida; a role é OR sobre o conjunto do nível.
        if (!currentUser.IsAuthenticated)
        {
            await AuditAsync(descriptor, "Denied", "Autenticação é necessária para executar tools de IA.", cancellationToken);
            throw new AiToolAuthorizationException("Autenticação é necessária para executar tools de IA.");
        }

        var requiredRoles = descriptor.Access.RequiredRoles();
        var grantedRoles = currentUser.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requiredRoles.Count > 0 && !grantedRoles.Overlaps(requiredRoles))
        {
            var missing = string.Join(", ", requiredRoles.OrderBy(role => role, StringComparer.Ordinal));
            await AuditAsync(descriptor, "Denied", $"Roles ausentes: {missing}", cancellationToken);
            throw new AiToolAuthorizationException($"Roles ausentes: {missing}");
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(descriptor.Timeout);

            AiToolResult result;
            try
            {
                result = await tool.ExecuteAsync(arguments, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout da tool (não do chamador): §52.
                throw new AiToolTimeoutException(descriptor.Name, descriptor.Timeout);
            }

            // §52: o executor garante o teto de resultados mesmo se a
            // tool esquecer de aplicar.
            if (result.Findings.Count > descriptor.MaxResults)
            {
                result = result with { Findings = result.Findings.Take(descriptor.MaxResults).ToList() };
            }

            await AuditAsync(descriptor, "Succeeded", null, cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await AuditAsync(descriptor, "Failed", exception.Message, cancellationToken);
            throw;
        }
    }

    private async Task AuditAsync(
        AiToolDescriptor descriptor,
        string outcome,
        string? error,
        CancellationToken cancellationToken)
    {
        var entry = new AuditEntry(
            $"AI.Tool.{descriptor.Name}",
            descriptor.Source,
            outcome,
            actorId: currentUser.UserName,
            correlationId: Activity.Current?.TraceId.ToString(),
            error: error);

        await auditWriter.WriteAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Tool de IA {Tool} {Outcome} (ator={Actor}, fonte={Source}).",
            descriptor.Name, outcome, currentUser.UserName, descriptor.Source);
    }
}

/// <summary>Executor de tools de IA (contrato consumível sem depender da implementação).</summary>
public interface IAiToolExecutor
{
    Task<AiToolResult> ExecuteAsync(
        string toolName,
        AiToolArguments arguments,
        CancellationToken cancellationToken = default);
}
