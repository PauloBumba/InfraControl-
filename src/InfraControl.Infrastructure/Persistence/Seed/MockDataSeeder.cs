using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Persistence.Seed;

/// <summary>Contagem do que foi CRIADO numa rodada (skips não contam).</summary>
public sealed record MockDataSeedResult(
    int UsersCreated,
    int LocationsCreated,
    int ServicesCreated,
    int DevicesCreated,
    int IncidentsCreated,
    int AlertsCreated,
    int TasksCreated,
    int AuditEntriesCreated);

/// <summary>
/// F46-T001 — seed determinístico de dados mock para o diagnóstico
/// ponta-a-ponta (front → API → banco). Espelha o padrão dos seeders do
/// boot (AdminBootstrapper/AiProviderSeeder): idempotente por entidade e
/// nunca toca em dado sem a marcação. Cada item só é criado se o registro
/// marcado equivalente não existir; <see cref="PurgeAsync"/> remove apenas
/// o marcado, na ordem inversa das FKs.
/// </summary>
public sealed class MockDataSeeder(
    AppDbContext dbContext,
    UserManager<InfraControlUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
{
    private static readonly string MockEmailSuffix = "@" + MockDataOptions.EmailDomain;

    private sealed record UserSpec(string Email, string DisplayName, UserRole Role);

    private static readonly UserSpec[] Users =
    [
        new($"admin{MockEmailSuffix}", $"{MockDataOptions.Marker} Admin", UserRole.Admin),
        new($"operator{MockEmailSuffix}", $"{MockDataOptions.Marker} Operador", UserRole.Operator),
        new($"viewer{MockEmailSuffix}", $"{MockDataOptions.Marker} Observador", UserRole.Viewer)
    ];

    public async Task<MockDataSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var usersCreated = await SeedUsersAsync(cancellationToken);

        var locationsCreated = 0;
        foreach (var (name, description) in new[]
        {
            ($"{MockDataOptions.Marker} Datacenter SP", "Local mock — rack A"),
            ($"{MockDataOptions.Marker} Escritório RJ", "Local mock — andar 4")
        })
        {
            if (await dbContext.Locations.AnyAsync(
                    l => l.Name == name, cancellationToken))
                continue;
            dbContext.Locations.Add(Location.Create(name, description));
            locationsCreated++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var servicesCreated = 0;
        foreach (var name in new[]
        {
            $"{MockDataOptions.Marker} ERP",
            $"{MockDataOptions.Marker} VPN Corporativa"
        })
        {
            if (await dbContext.Services.AnyAsync(
                    s => s.Name == name, cancellationToken))
                continue;
            dbContext.Services.Add(Service.Create(name, "Serviço mock de diagnóstico"));
            servicesCreated++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var locations = await dbContext.Locations
            .Where(l => l.Name.StartsWith(MockDataOptions.Marker))
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);
        var spLocation = locations[0];
        var rjLocation = locations[^1];

        var devicesCreated = 0;
        var deviceSpecs = new (string Name, DeviceType Type, string Ip, Guid Location)[]
        {
            ($"{MockDataOptions.Marker} sw-core-01", DeviceType.Switch, "10.99.0.10", spLocation.Id),
            ($"{MockDataOptions.Marker} sw-access-02", DeviceType.Switch, "10.99.0.11", spLocation.Id),
            ($"{MockDataOptions.Marker} rt-border-01", DeviceType.Router, "10.99.0.1", spLocation.Id),
            ($"{MockDataOptions.Marker} fw-edge-01", DeviceType.Firewall, "10.99.0.2", spLocation.Id),
            ($"{MockDataOptions.Marker} srv-app-01", DeviceType.Server, "10.99.1.10", spLocation.Id),
            ($"{MockDataOptions.Marker} srv-db-01", DeviceType.Server, "10.99.1.11", spLocation.Id),
            ($"{MockDataOptions.Marker} sw-rj-01", DeviceType.Switch, "10.99.2.10", rjLocation.Id),
            ($"{MockDataOptions.Marker} pc-dev-01", DeviceType.Workstation, "10.99.2.50", rjLocation.Id)
        };
        foreach (var (name, type, ip, locationId) in deviceSpecs)
        {
            if (await dbContext.Devices.AnyAsync(
                    d => d.Name == name, cancellationToken))
                continue;
            dbContext.Devices.Add(Device.Create(locationId, name, type, ip: ip));
            devicesCreated++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var devices = await dbContext.Devices
            .Where(d => d.Name.StartsWith(MockDataOptions.Marker))
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        var operatorId = await ResolveMockUserIdAsync($"operator{MockEmailSuffix}", cancellationToken);

        var incidentsCreated = 0;
        var incidentSpecs = new (string Title, IncidentSeverity Severity, int StatusIndex, string? DeviceName)[]
        {
            ($"{MockDataOptions.Marker} Perda de link MPLS", IncidentSeverity.Critical, 0, $"{MockDataOptions.Marker} rt-border-01"),
            ($"{MockDataOptions.Marker} CPU acima de 90% no srv-db", IncidentSeverity.High, 1, $"{MockDataOptions.Marker} srv-db-01"),
            ($"{MockDataOptions.Marker} Espaço em disco crítico", IncidentSeverity.Medium, 2, $"{MockDataOptions.Marker} srv-app-01"),
            ($"{MockDataOptions.Marker} Flap de porta no sw-rj", IncidentSeverity.Low, 0, $"{MockDataOptions.Marker} sw-rj-01")
        };
        var statuses = new[]
        {
            IncidentStatus.Open,
            IncidentStatus.Investigating,
            IncidentStatus.Identified,
            IncidentStatus.Open
        };
        foreach (var (title, severity, statusIndex, deviceName) in incidentSpecs)
        {
            if (await dbContext.Incidents.AnyAsync(
                    i => i.Title == title, cancellationToken))
                continue;
            var device = deviceName is null
                ? null
                : devices.FirstOrDefault(d => d.Name == deviceName);
            var incident = Incident.Create(
                title, severity, device?.Id,
                $"{MockDataOptions.Marker} incidente de diagnóstico");
            if (statuses[statusIndex] != IncidentStatus.Open)
                incident.ChangeStatus(statuses[statusIndex]);
            dbContext.Incidents.Add(incident);
            incidentsCreated++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var alertsCreated = 0;
        var alertSpecs = new (string Message, AlertSeverity Severity, string DeviceName)[]
        {
            ($"{MockDataOptions.Marker} Ping sem resposta (sw-core-01)", AlertSeverity.Critical, $"{MockDataOptions.Marker} sw-core-01"),
            ($"{MockDataOptions.Marker} Latência alta no link (rt-border)", AlertSeverity.Warning, $"{MockDataOptions.Marker} rt-border-01"),
            ($"{MockDataOptions.Marker} Certificado expira em 7 dias", AlertSeverity.Warning, $"{MockDataOptions.Marker} fw-edge-01")
        };
        foreach (var (message, severity, deviceName) in alertSpecs)
        {
            if (await dbContext.Alerts.AnyAsync(
                    a => a.Message == message, cancellationToken))
                continue;
            var device = devices.First(d => d.Name == deviceName);
            dbContext.Alerts.Add(Alert.Create(
                device.Id, CheckKind.Ping, severity, message));
            alertsCreated++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var tasksCreated = 0;
        var taskSpecs = new (string Title, TaskKind Kind, TaskPriority Priority)[]
        {
            ($"{MockDataOptions.Marker} Aplicar patch no srv-app", TaskKind.Maintenance, TaskPriority.High),
            ($"{MockDataOptions.Marker} Revisar política de backup", TaskKind.Preventive, TaskPriority.Medium),
            ($"{MockDataOptions.Marker} Trocar switch de acesso RJ", TaskKind.Change, TaskPriority.Low),
            ($"{MockDataOptions.Marker} Investigar flap sw-rj", TaskKind.Incident, TaskPriority.Critical)
        };
        foreach (var (title, kind, priority) in taskSpecs)
        {
            if (await dbContext.Tasks.AnyAsync(
                    t => t.Title == title, cancellationToken))
                continue;
            dbContext.Tasks.Add(TaskItem.Create(
                title, kind,
                $"{MockDataOptions.Marker} task de diagnóstico",
                priority,
                responsibleId: operatorId,
                createdById: operatorId));
            tasksCreated++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var auditCreated = 0;
        if (!await dbContext.AuditEntries.AnyAsync(
                a => a.Action == "MockDataSeed", cancellationToken))
        {
            dbContext.AuditEntries.Add(new AuditEntry(
                "MockDataSeed", "MockData", "Succeeded",
                operatorId?.ToString()));
            auditCreated++;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MockDataSeedResult(
            usersCreated, locationsCreated, servicesCreated, devicesCreated,
            incidentsCreated, alertsCreated, tasksCreated, auditCreated);
    }

    /// <summary>
    /// Remove APENAS o que tem a marcação (nomes <c>[MOCK]</c>, e-mails
    /// <c>@mock.test</c>, action <c>MockDataSeed</c>), na ordem inversa das
    /// FKs. Idempotente: segunda rodada devolve 0.
    /// </summary>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var removed = 0;

        var audits = await dbContext.AuditEntries
            .Where(a => a.Action == "MockDataSeed")
            .ToListAsync(cancellationToken);
        dbContext.AuditEntries.RemoveRange(audits);
        removed += audits.Count;

        var tasks = await dbContext.Tasks
            .Where(t => t.Title.StartsWith(MockDataOptions.Marker))
            .ToListAsync(cancellationToken);
        dbContext.Tasks.RemoveRange(tasks);
        removed += tasks.Count;

        var alerts = await dbContext.Alerts
            .Where(a => a.Message.StartsWith(MockDataOptions.Marker))
            .ToListAsync(cancellationToken);
        dbContext.Alerts.RemoveRange(alerts);
        removed += alerts.Count;

        var incidents = await dbContext.Incidents
            .Where(i => i.Title.StartsWith(MockDataOptions.Marker))
            .ToListAsync(cancellationToken);
        dbContext.Incidents.RemoveRange(incidents);
        removed += incidents.Count;

        var devices = await dbContext.Devices
            .Where(d => d.Name.StartsWith(MockDataOptions.Marker))
            .ToListAsync(cancellationToken);
        dbContext.Devices.RemoveRange(devices);
        removed += devices.Count;

        var locations = await dbContext.Locations
            .Where(l => l.Name.StartsWith(MockDataOptions.Marker))
            .ToListAsync(cancellationToken);
        dbContext.Locations.RemoveRange(locations);
        removed += locations.Count;

        var services = await dbContext.Services
            .Where(s => s.Name.StartsWith(MockDataOptions.Marker))
            .ToListAsync(cancellationToken);
        dbContext.Services.RemoveRange(services);
        removed += services.Count;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Usuários mock por último (demais tabelas já não dependem deles).
        var mockEmails = Users.Select(u => u.Email).ToList();
        var domainUsers = await dbContext.DomainUsers
            .Where(u => mockEmails.Contains(u.Email))
            .ToListAsync(cancellationToken);
        var userIds = domainUsers.Select(u => u.Id).ToList();

        if (userIds.Count > 0)
        {
            dbContext.UserSessions.RemoveRange(
                await dbContext.UserSessions
                    .Where(s => userIds.Contains(s.UserId))
                    .ToListAsync(cancellationToken));
            dbContext.UserSettings.RemoveRange(
                await dbContext.UserSettings
                    .Where(s => userIds.Contains(s.UserId))
                    .ToListAsync(cancellationToken));
            dbContext.UserRoleAssignments.RemoveRange(
                await dbContext.UserRoleAssignments
                    .Where(r => userIds.Contains(r.UserId))
                    .ToListAsync(cancellationToken));
            dbContext.NotificationPreferences.RemoveRange(
                await dbContext.NotificationPreferences
                    .Where(p => userIds.Contains(p.UserId))
                    .ToListAsync(cancellationToken));
            dbContext.NotificationRecipients.RemoveRange(
                await dbContext.NotificationRecipients
                    .Where(r => r.UserId != null && userIds.Contains(r.UserId.Value))
                    .ToListAsync(cancellationToken));

            dbContext.DomainUsers.RemoveRange(domainUsers);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var email in mockEmails)
        {
            var identityUser = await userManager.FindByEmailAsync(email);
            if (identityUser is null)
                continue;
            var result = await userManager.DeleteAsync(identityUser);
            if (result.Succeeded)
                removed++;
        }

        return removed;
    }

    private async Task<int> SeedUsersAsync(CancellationToken cancellationToken)
    {
        var created = 0;

        foreach (var spec in Users)
        {
            if (!await roleManager.RoleExistsAsync(spec.Role.ToString()))
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>(spec.Role.ToString()));

            var identityUser = await userManager.FindByEmailAsync(spec.Email);
            if (identityUser is null)
            {
                identityUser = new InfraControlUser
                {
                    Id = Guid.NewGuid(),
                    Email = spec.Email,
                    UserName = spec.Email,
                    EmailConfirmed = true
                };
                var createResult = await userManager.CreateAsync(
                    identityUser, MockDataOptions.Password);
                if (!createResult.Succeeded)
                    throw new InvalidOperationException(
                        $"MockDataSeeder não criou {spec.Email}: " +
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));

                await userManager.AddToRoleAsync(identityUser, spec.Role.ToString());
                created++;
            }

            // Espelho de domínio (E2EHost faz o mesmo): a autorização e o
            // /auth/me leem a linha de domínio + roles dela.
            if (!await dbContext.DomainUsers.AnyAsync(
                    u => u.Id == identityUser.Id, cancellationToken))
            {
                dbContext.DomainUsers.Add(User.Create(
                    identityUser.Id, spec.Email, spec.DisplayName, spec.Role));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return created;
    }

    private async Task<Guid?> ResolveMockUserIdAsync(
        string email, CancellationToken cancellationToken)
    {
        var identity = await userManager.FindByEmailAsync(email);
        if (identity is null)
            return null;
        var domain = await dbContext.DomainUsers
            .FirstOrDefaultAsync(u => u.Id == identity.Id, cancellationToken);
        return domain?.Id;
    }
}
