using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Auth;

/// <summary>
/// F1-T016c: audit manual dos eventos de segurança que NÃO passam pelo pipeline
/// MediatR (login, MFA e sessões são chamadas diretas do controller, sem o
/// AuditBehavior). Só metadados — action, resource, outcome, actor (e-mail do
/// dono) e correlation (TraceId); nenhum código, senha ou secret entra na linha.
/// Chamado ANTES do SaveChanges da operação: efeito e audit commitam juntos,
/// na mesma transação.
/// </summary>
internal static class SecurityAudit
{
    /// <summary>Versão p/ quando o e-mail do dono já está em mão (login).</summary>
    internal static Task WriteAsync(
        IAuditWriter auditWriter,
        string action,
        string resource,
        string? actorId,
        CancellationToken cancellationToken)
        => auditWriter.WriteAsync(
            new AuditEntry(action, resource, "Succeeded", actorId, Activity.Current?.TraceId.ToString()),
            cancellationToken);

    /// <summary>Versão p/ resolver o ator pelo dono da ação (auto-serviço).</summary>
    internal static async Task WriteAsync(
        IAuditWriter auditWriter,
        AppDbContext dbContext,
        string action,
        string resource,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var actorId = await dbContext.DomainUsers.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);
        await WriteAsync(auditWriter, action, resource, actorId, cancellationToken);
    }
}
