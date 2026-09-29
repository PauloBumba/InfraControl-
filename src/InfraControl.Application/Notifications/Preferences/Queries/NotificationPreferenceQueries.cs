using InfraControl.Application.Notifications.Preferences.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Notifications.Preferences.Queries;

// Sem cache (IQuery direto): preferências mudam por upsert do usuário.

public sealed record SearchNotificationPreferencesQuery(Guid UserId)
    : IQuery<IReadOnlyCollection<NotificationPreferenceDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class SearchNotificationPreferencesQueryHandler(
    INotificationPreferenceRepository repository)
    : IRequestHandler<SearchNotificationPreferencesQuery, IReadOnlyCollection<NotificationPreferenceDto>>
{
    public async Task<IReadOnlyCollection<NotificationPreferenceDto>> Handle(
        SearchNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferences = await repository.ListByUserIdAsync(
            request.UserId, cancellationToken);

        return preferences
            .Select(p => new NotificationPreferenceDto(p.UserId, p.Channel, p.Enabled))
            .ToArray();
    }
}
