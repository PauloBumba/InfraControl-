using InfraControl.Application.Operations.Backup.Ports;
using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Retention;

/// <summary>
/// Cadastra ou atualiza a política de retenção do tipo (F12-T002) — o
/// cleanup passa a usar esses dias no lugar do default de config.
/// </summary>
public sealed record UpsertRetentionPolicyCommand(RetentionDataType DataType, int Days)
    : BaseCommand<RetentionPolicyDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "RetentionPolicy";
}

/// <summary>
/// Remove a política de retenção do tipo (F12-T002) — o cleanup volta ao
/// default de <c>Retention:*</c>.
/// </summary>
public sealed record DeleteRetentionPolicyCommand(RetentionDataType DataType)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "RetentionPolicy";
}

public sealed class UpsertRetentionPolicyCommandHandler(
    IRetentionPolicyRepository repository,
    IBackupSettingsRepository backupSettings)
    : IRequestHandler<UpsertRetentionPolicyCommand, RetentionPolicyDto>
{
    public async Task<RetentionPolicyDto> Handle(
        UpsertRetentionPolicyCommand request, CancellationToken cancellationToken)
    {
        // Backups = mesmo valor do /settings/backup (F50): grava o
        // singleton de settings, nunca uma row em retention_policies.
        if (request.DataType == RetentionDataType.Backups)
        {
            var settings = await backupSettings.GetAsync(cancellationToken);
            if (settings is null)
            {
                settings = BackupSettings.Create(
                    enabled: true, intervalHours: 24, retentionDays: request.Days,
                    path: "backups", fileNamePrefix: "infracontrol", timeoutSeconds: 3600);
                await backupSettings.AddAsync(settings, cancellationToken);
            }
            else
            {
                settings.Update(
                    settings.Enabled, settings.IntervalHours, request.Days,
                    settings.Path, settings.FileNamePrefix, settings.TimeoutSeconds);
            }

            await backupSettings.SaveChangesAsync(cancellationToken);
            return new RetentionPolicyDto("Backups", request.Days, "BackupSettings");
        }

        var existing = await repository.GetByTypeAsync(request.DataType, cancellationToken);
        if (existing is null)
        {
            var policy = RetentionPolicy.Create(request.DataType, request.Days);
            await repository.AddAsync(policy, cancellationToken);
            return new RetentionPolicyDto(request.DataType.ToString(), policy.Days, "Policy");
        }

        existing.Update(request.Days);
        await repository.AddAsync(existing, cancellationToken);
        return new RetentionPolicyDto(request.DataType.ToString(), existing.Days, "Policy");
    }
}

public sealed class DeleteRetentionPolicyCommandHandler(
    IRetentionPolicyRepository repository,
    IBackupSettingsRepository backupSettings)
    : IRequestHandler<DeleteRetentionPolicyCommand>
{
    public async Task Handle(
        DeleteRetentionPolicyCommand request, CancellationToken cancellationToken)
    {
        // Backups restaura o default do BackupSettings (7 dias).
        if (request.DataType == RetentionDataType.Backups)
        {
            var settings = await backupSettings.GetAsync(cancellationToken);
            if (settings is not null && settings.RetentionDays != 7)
            {
                settings.Update(
                    settings.Enabled, settings.IntervalHours, 7,
                    settings.Path, settings.FileNamePrefix, settings.TimeoutSeconds);
                await backupSettings.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var deleted = await repository.DeleteAsync(request.DataType, cancellationToken);
        if (!deleted)
            throw new KeyNotFoundException(
                $"Política de retenção '{request.DataType}' não encontrada.");
    }
}
