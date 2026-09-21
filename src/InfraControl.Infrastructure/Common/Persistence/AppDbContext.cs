using System.Reflection;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Common.Persistence;

public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator? _mediator;

    public AppDbContext(DbContextOptions<AppDbContext> options, IMediator? mediator = null)
        : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();

        if (_mediator != null)
        {
            await _mediator.DispatchDomainEventsAsync(this);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditFields()
    {
        var now = AppTimeProvider.Current.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreateDateTime = now;
                entry.Entity.UpdateDateTime = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdateDateTime = now;
            }
        }
    }
}
