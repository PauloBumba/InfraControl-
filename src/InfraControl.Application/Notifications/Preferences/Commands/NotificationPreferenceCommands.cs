using InfraControl.Application.Notifications.Preferences.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Notifications.Preferences.Commands;

// F9-T003: preferências de notificação por usuário/canal — upsert do
// estado desejado; 404 (usuário/preferência), 400 (canal inválido).

public sealed record SetNotificationPreferenceCommand(
    Guid UserId,
    NotificationChannel Channel,
    bool Enabled) : BaseCommand<NotificationPreferenceDto>, IAuthorizedRequest
{
    // Escrita = nível Operar (decisão F9-T003: a ADR-016 não separa
    // "próprio vs alheio"; gestão de preferência é escrita).
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SetNotificationPreferenceCommandHandler(
    INotificationPreferenceRepository repository,
    ILogger<SetNotificationPreferenceCommandHandler> logger)
    : IRequestHandler<SetNotificationPreferenceCommand, NotificationPreferenceDto>
{
    public async Task<NotificationPreferenceDto> Handle(
        SetNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.UserExistsAsync(request.UserId, cancellationToken))
            throw new KeyNotFoundException($"Usuário '{request.UserId}' não encontrado.");

        // Entity valida canal inválido (ArgumentException → 400 pt-BR).
        var existing = await repository.FindAsync(
            request.UserId, request.Channel, cancellationToken);

        var isNew = existing is null;
        var preference = existing ?? NotificationPreference.Create(
            request.UserId, request.Channel, request.Enabled);

        preference.SetEnabled(request.Enabled);

        if (isNew)
            await repository.AddAsync(preference, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Preferência {Channel} do usuário {UserId} definida como {Enabled}.",
            request.Channel, request.UserId, request.Enabled);

        return new NotificationPreferenceDto(
            preference.UserId, preference.Channel, preference.Enabled);
    }
}

public sealed record DeleteNotificationPreferenceCommand(
    Guid UserId,
    NotificationChannel Channel) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class DeleteNotificationPreferenceCommandHandler(
    INotificationPreferenceRepository repository,
    ILogger<DeleteNotificationPreferenceCommandHandler> logger)
    : IRequestHandler<DeleteNotificationPreferenceCommand>
{
    public async Task Handle(
        DeleteNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        var preference = await repository.FindAsync(
                request.UserId, request.Channel, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Preferência de notificação '{request.Channel}' do usuário '{request.UserId}' não encontrada.");

        repository.Remove(preference);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Preferência {Channel} do usuário {UserId} removida.",
            request.Channel, request.UserId);
    }
}
