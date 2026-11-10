using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Operations.Backup;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// Configuração de backup editável pela plataforma (spec evolução §3/§9):
/// singleton no banco que sobrepõe <c>Backup:*</c> do appsettings em
/// runtime — o BackupWorker rele o override a cada ciclo. Somente Admin.
/// </summary>
[Route("api/v1/operations/backup-settings")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class BackupSettingsController(ISender sender) : BaseController
{
    /// <summary>Estado efetivo (override do banco ou defaults do appsettings).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetBackupSettingsQuery(), cancellationToken)));

    /// <summary>Salva a configuração (upsert do singleton).</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertBackupSettingsRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new UpsertBackupSettingsRequest();
        return Ok(ApiResponse.Ok(await sender.Send(
            new UpsertBackupSettingsCommand(
                request.Enabled, request.IntervalHours, request.RetentionDays,
                request.Path, request.FileNamePrefix, request.TimeoutSeconds),
            cancellationToken)));
    }
}

public sealed class UpsertBackupSettingsRequest
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 7;
    public string Path { get; set; } = "backups";
    public string FileNamePrefix { get; set; } = "infracontrol";
    public int TimeoutSeconds { get; set; } = 3600;
}
