using System.Reflection;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Outbox;
using InfraControl.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DomainIdentity = InfraControl.Domain.Identity;

namespace InfraControl.Infrastructure.Common.Persistence;

public class AppDbContext : IdentityDbContext<InfraControlUser, IdentityRole<Guid>, Guid>, IUnitOfWork
{
    private readonly IMediator? _mediator;

    public AppDbContext(DbContextOptions<AppDbContext> options, IMediator? mediator = null)
        : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<DomainIdentity.User> DomainUsers => Set<DomainIdentity.User>();
    public DbSet<DomainIdentity.UserRoleAssignment> UserRoleAssignments => Set<DomainIdentity.UserRoleAssignment>();
    public DbSet<DomainIdentity.UserSession> UserSessions => Set<DomainIdentity.UserSession>();

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
