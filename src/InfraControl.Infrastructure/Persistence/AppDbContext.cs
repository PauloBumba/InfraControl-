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
    public DbSet<ExternalDeviceReference> ExternalDeviceReferences => Set<ExternalDeviceReference>();
    public DbSet<UniFiIntegrationSetting> UniFiIntegrationSettings => Set<UniFiIntegrationSetting>();
    public DbSet<ZabbixIntegrationSetting> ZabbixIntegrationSettings => Set<ZabbixIntegrationSetting>();
    public DbSet<ZabbixHost> ZabbixHosts => Set<ZabbixHost>();
    public DbSet<UniFiSite> UniFiSites => Set<UniFiSite>();
    public DbSet<AiProvider> AiProviders => Set<AiProvider>();
    public DbSet<AiProviderCapability> AiProviderCapabilities => Set<AiProviderCapability>();
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
    public DbSet<TaskKnowledgeArticle> TaskKnowledgeArticles => Set<TaskKnowledgeArticle>();
    public DbSet<TaskPdi> TaskPdis => Set<TaskPdi>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskCriterion> TaskCriteria => Set<TaskCriterion>();
    public DbSet<TaskImpediment> TaskImpediments => Set<TaskImpediment>();
    public DbSet<TaskEvidence> TaskEvidences => Set<TaskEvidence>();
    public DbSet<TaskPhase> TaskPhases => Set<TaskPhase>();
    public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<TaskSla> TaskSlas => Set<TaskSla>();
    public DbSet<TaskSlaPause> TaskSlasPauses => Set<TaskSlaPause>();
    public DbSet<Slo> Slos => Set<Slo>();
    public DbSet<SloMeasurement> SloMeasurements => Set<SloMeasurement>();
    public DbSet<SloBudget> SloBudgets => Set<SloBudget>();
    public DbSet<DiscoveryRun> DiscoveryRuns => Set<DiscoveryRun>();
    public DbSet<DiscoveryResult> DiscoveryResults => Set<DiscoveryResult>();
    public DbSet<MonitoringCheck> MonitoringChecks => Set<MonitoringCheck>();
    public DbSet<MonitoringResult> MonitoringResults => Set<MonitoringResult>();
    public DbSet<MonitoringEvent> MonitoringEvents => Set<MonitoringEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<KnowledgeCategory> KnowledgeCategories => Set<KnowledgeCategory>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<KnowledgeArticleVersion> KnowledgeArticleVersions => Set<KnowledgeArticleVersion>();
    public DbSet<KnowledgeArticleOrigin> KnowledgeArticleOrigins => Set<KnowledgeArticleOrigin>();
    public DbSet<KnowledgeArticleDevice> KnowledgeArticleDevices => Set<KnowledgeArticleDevice>();
    public DbSet<KnowledgeArticleIncident> KnowledgeArticleIncidents => Set<KnowledgeArticleIncident>();
    public DbSet<KnowledgeArticleService> KnowledgeArticleServices => Set<KnowledgeArticleService>();
    public DbSet<RemoteSession> RemoteSessions => Set<RemoteSession>();
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();
    public DbSet<CameraNvrMapping> CameraNvrMappings => Set<CameraNvrMapping>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<DomainIdentity.User> DomainUsers => Set<DomainIdentity.User>();
    public DbSet<DomainIdentity.UserRoleAssignment> UserRoleAssignments => Set<DomainIdentity.UserRoleAssignment>();
    public DbSet<DomainIdentity.UserSession> UserSessions => Set<DomainIdentity.UserSession>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<DomainIdentity.UserMfa> UserMfas => Set<DomainIdentity.UserMfa>();
    public DbSet<IndividualDevelopmentPlan> IndividualDevelopmentPlans => Set<IndividualDevelopmentPlan>();
    public DbSet<PdiHistoryEntry> PdiHistoryEntries => Set<PdiHistoryEntry>();
    public DbSet<PdiCompetency> PdiCompetencies => Set<PdiCompetency>();
    public DbSet<PdiObjective> PdiObjectives => Set<PdiObjective>();
    public DbSet<PdiStrategy> PdiStrategies => Set<PdiStrategy>();
    public DbSet<PdiPhase> PdiPhases => Set<PdiPhase>();
    public DbSet<PdiActivity> PdiActivities => Set<PdiActivity>();
    public DbSet<PdiProgress> PdiProgresses => Set<PdiProgress>();
    public DbSet<UniFiSyncRun> UniFiSyncRuns => Set<UniFiSyncRun>();
    public DbSet<PdiCheckIn> PdiCheckIns => Set<PdiCheckIn>();
    public DbSet<PdiImpediment> PdiImpediments => Set<PdiImpediment>();
    public DbSet<PdiFeedback> PdiFeedbacks => Set<PdiFeedback>();
    public DbSet<PdiEvidence> PdiEvidences => Set<PdiEvidence>();
    public DbSet<PdiAssessment> PdiAssessments => Set<PdiAssessment>();
    public DbSet<PdiResult> PdiResults => Set<PdiResult>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectPhase> ProjectPhases => Set<ProjectPhase>();
    public DbSet<ProjectProgress> ProjectProgresses => Set<ProjectProgress>();
    public DbSet<ProjectImpediment> ProjectImpediments => Set<ProjectImpediment>();
    public DbSet<ProjectEvidence> ProjectEvidences => Set<ProjectEvidence>();
    public DbSet<ProjectHistoryEntry> ProjectHistoryEntries => Set<ProjectHistoryEntry>();

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
