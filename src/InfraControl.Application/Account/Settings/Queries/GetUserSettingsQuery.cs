using InfraControl.Application.Account.Settings.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Account.Settings.Queries;

/// <summary>
/// Preferências do usuário autenticado (Fase 06d). Lazy-create: se o usuário
/// ainda não tem settings, devolve os defaults sem gravar.
/// Sem cache (IQuery direto): o PUT faria o GET devolver valor velho por até
/// CacheDuration — mesma decisão das notification preferences.
/// </summary>
public sealed record GetUserSettingsQuery(Guid UserId)
    : IQuery<UserSettingsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetUserSettingsQueryHandler(IUserSettingsRepository repository)
    : IRequestHandler<GetUserSettingsQuery, UserSettingsDto>
{
    public async Task<UserSettingsDto> Handle(
        GetUserSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await repository.FindByUserIdAsync(request.UserId, cancellationToken)
            ?? UserSettings.CreateDefault(request.UserId);

        return new UserSettingsDto(
            settings.UserId,
            settings.Timezone,
            settings.DarkMode,
            settings.AutoRefresh,
            settings.Telemetry);
    }
}
