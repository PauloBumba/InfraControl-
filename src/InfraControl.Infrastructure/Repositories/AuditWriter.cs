using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;

namespace InfraControl.Infrastructure.Repositories;

public sealed class AuditWriter(AppDbContext dbContext) : IAuditWriter
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        => await dbContext.AuditEntries.AddAsync(entry, cancellationToken);
}
