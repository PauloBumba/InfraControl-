using InfraControl.Domain.Entities;

namespace InfraControl.Application.Common.Interfaces;

public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
