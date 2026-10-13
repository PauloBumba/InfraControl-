using InfraControl.Application.Account.Settings.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Account.Settings.Commands;

/// <summary>
/// Upsert das preferências do usuário autenticado (Fase 06d). Lazy-create:
/// grava os defaults e sobrescreve com o payload.
/// </summary>
public sealed record UpdateUserSettingsCommand(
    Guid UserId,
    string Timezone,
    bool DarkMode,
    bool AutoRefresh,
    bool Telemetry) : BaseCommand<UserSettingsDto>, IAuthorizedRequest
{
    // Settings próprio = preferência pessoal, qualquer role autenticada
    // (mesmo contrato do PUT /auth/profile).
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class UpdateUserSettingsCommandHandler(
    IUserSettingsRepository repository,
    ILogger<UpdateUserSettingsCommandHandler> logger)
    : IRequestHandler<UpdateUserSettingsCommand, UserSettingsDto>
{
    public async Task<UserSettingsDto> Handle(
        UpdateUserSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await repository.FindByUserIdAsync(request.UserId, cancellationToken);

        var isNew = settings is null;
        settings ??= UserSettings.CreateDefault(request.UserId);

        // Entity valida timezone (ArgumentException → 400 pt-BR).
        settings.Update(
            request.Timezone,
            request.DarkMode,
            request.AutoRefresh,
            request.Telemetry);

        if (isNew)
            await repository.AddAsync(settings, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Settings do usuário {UserId} atualizados (timezone={Timezone}).",
            request.UserId, settings.Timezone);

        return new UserSettingsDto(
            settings.UserId,
            settings.Timezone,
            settings.DarkMode,
            settings.AutoRefresh,
            settings.Telemetry);
    }
}
