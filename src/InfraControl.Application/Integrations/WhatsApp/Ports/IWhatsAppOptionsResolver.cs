using InfraControl.Application.Notifications.Channels;

namespace InfraControl.Application.Integrations.WhatsApp.Ports;

/// <summary>
/// ADR-062: opções efetivas do canal WhatsApp (banco → env). Devolve o
/// <see cref="WhatsAppChannelOptions"/> resolvido: quando existe linha no
/// banco usa-a (GroupId escolhido na UI, BaseUrl/ApiKey/InstanceId
/// persistidas); sem linha cai no env (Notifications:Channels:WhatsApp).
/// </summary>
public interface IWhatsAppOptionsResolver
{
    Task<WhatsAppChannelOptions> ResolveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Resolver estático para testes e compatibilidade com IOptions.
/// </summary>
public sealed class StaticWhatsAppOptionsResolver(WhatsAppChannelOptions options) : IWhatsAppOptionsResolver
{
    public Task<WhatsAppChannelOptions> ResolveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(options);
}

/// <summary>
/// ADR-062: opções WhatsApp vindas de env/config (fallback quando não existe
/// linha no banco). Implementação em Infrastructure envolve
/// <c>IOptions&lt;NotificationChannelsOptions&gt;</c> para o Application não
/// depender de Options.
/// </summary>
public interface IWhatsAppEnvOptionsAccessor
{
    WhatsAppChannelOptions Current { get; }
}
