using System.Reflection;
using InfraControl.Application.Common.Interfaces;
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
        if (_mediator != null)
        {
            await _mediator.DispatchDomainEventsAsync(this);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
