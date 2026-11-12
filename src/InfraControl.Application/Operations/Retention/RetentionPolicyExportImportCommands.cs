using InfraControl.Application.Operations.Backup.Ports;
using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Retention;

/// <summary>Item do export/import JSON das políticas (F50).</summary>
public sealed record RetentionExportItem(string DataType, int Days);

/// <summary>Arquivo de export: todas as linhas com o valor efetivo.</summary>
public sealed record RetentionExportDto(
    DateTime ExportedAtUtc,
    IReadOnlyList<RetentionExportItem> Policies);

/// <summary>Resultado do import: itens validados e aplicados.</summary>
public sealed record RetentionImportResultDto(int Imported);

/// <summary>Exporta o estado efetivo de todas as políticas (JSON).</summary>
public sealed record ExportRetentionPoliciesQuery
    : IQuery<RetentionExportDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "RetentionPolicy";
}

public sealed class ExportRetentionPoliciesQueryHandler(
    GetRetentionPoliciesQueryHandler effectivePolicies)
    : IRequestHandler<ExportRetentionPoliciesQuery, RetentionExportDto>
{
    public async Task<RetentionExportDto> Handle(
        ExportRetentionPoliciesQuery request, CancellationToken cancellationToken)
    {
        var dtos = await effectivePolicies.Handle(
            new GetRetentionPoliciesQuery(), cancellationToken);

        return new RetentionExportDto(
            DateTime.UtcNow,
            dtos.Select(dto => new RetentionExportItem(dto.DataType, dto.Days))
                .ToArray());
    }
}

/// <summary>
/// Importa políticas em lote (F50): valida TODOS os itens antes de
/// aplicar qualquer um — payload inválido não deixa meio estado.
/// </summary>
public sealed record ImportRetentionPoliciesCommand(
    IReadOnlyList<RetentionExportItem> Policies)
    : BaseCommand<RetentionImportResultDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
    public string AuditResource => "RetentionPolicy";
}

public sealed class ImportRetentionPoliciesCommandHandler(
    IRetentionPolicyRepository repository,
    IBackupSettingsRepository backupSettings)
    : IRequestHandler<ImportRetentionPoliciesCommand, RetentionImportResultDto>
{
    public async Task<RetentionImportResultDto> Handle(
        ImportRetentionPoliciesCommand request, CancellationToken cancellationToken)
    {
        // 1) Validação integral primeiro (nada é gravado se algo falhar).
        var parsed = new List<(RetentionDataType DataType, int Days)>();
        foreach (var item in request.Policies)
        {
            if (!Enum.TryParse<RetentionDataType>(item.DataType, ignoreCase: true, out var dataType)
                || !Enum.IsDefined(dataType))
                throw new ArgumentException(
                    $"Tipo de dado de retenção inválido: '{item.DataType}'.", nameof(request));

            if (item.Days is < RetentionPolicy.MinDays or > RetentionPolicy.MaxDays)
                throw new ArgumentException(
                    $"Os dias da política '{item.DataType}' devem estar entre " +
                    $"{RetentionPolicy.MinDays} e {RetentionPolicy.MaxDays}.", nameof(request));

            parsed.Add((dataType, item.Days));
        }

        // 2) Aplicação.
        foreach (var (dataType, days) in parsed)
        {
            if (dataType == RetentionDataType.Backups)
            {
                var settings = await backupSettings.GetAsync(cancellationToken);
                if (settings is null)
                {
                    settings = BackupSettings.Create(
                        enabled: true, intervalHours: 24, retentionDays: days,
                        path: "backups", fileNamePrefix: "infracontrol", timeoutSeconds: 3600);
                    await backupSettings.AddAsync(settings, cancellationToken);
                }
                else
                {
                    settings.Update(
                        settings.Enabled, settings.IntervalHours, days,
                        settings.Path, settings.FileNamePrefix, settings.TimeoutSeconds);
                }

                continue;
            }

            var existing = await repository.GetByTypeAsync(dataType, cancellationToken);
            if (existing is null)
                await repository.AddAsync(
                    RetentionPolicy.Create(dataType, days), cancellationToken);
            else
            {
                existing.Update(days);
                await repository.AddAsync(existing, cancellationToken);
            }
        }

        await backupSettings.SaveChangesAsync(cancellationToken);
        return new RetentionImportResultDto(parsed.Count);
    }
}
