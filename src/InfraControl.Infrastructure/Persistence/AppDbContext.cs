using System.Reflection;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Entities;
using InfraControl.Domain.RemoteAccess;
using InfraControl.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DomainIdentity = InfraControl.Domain.Entities;

namespace InfraControl.Infrastructure.Persistence;

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
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<InvitationRole> InvitationRoles => Set<InvitationRole>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Rack> Racks => Set<Rack>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<NetworkInterface> NetworkInterfaces => Set<NetworkInterface>();
    public DbSet<MacAddress> MacAddresses => Set<MacAddress>();
    public DbSet<IpAddress> IpAddresses => Set<IpAddress>();
    public DbSet<SwitchPort> SwitchPorts => Set<SwitchPort>();
    public DbSet<Vlan> Vlans => Set<Vlan>();
    public DbSet<SwitchPortVlan> SwitchPortVlans => Set<SwitchPortVlan>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentAlert> IncidentAlerts => Set<IncidentAlert>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
    public DbSet<TaskDevice> TaskDevices => Set<TaskDevice>();
    public DbSet<TaskIncident> TaskIncidents => Set<TaskIncident>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<DiscoveryRun> DiscoveryRuns => Set<DiscoveryRun>();
    public DbSet<DiscoveryResult> DiscoveryResults => Set<DiscoveryResult>();
    public DbSet<MonitoringCheck> MonitoringChecks => Set<MonitoringCheck>();
    public DbSet<MonitoringResult> MonitoringResults => Set<MonitoringResult>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RemoteSession> RemoteSessions => Set<RemoteSession>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
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
