using InfraControl.Application.Integrations.WhatsApp.Ports;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Deliveries.Ports;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.Api.Workers;

/// <summary>
/// Notification Worker do canal WhatsApp (F9-T007, docs/07): despacha via
/// `IWhatsAppNotificationDispatcher` — ADR-059 §3 (F33-T003): destino é o
/// `GroupId` (broadcast no grupo) com fallback para o `Phone` individual.
/// ADR-062: o destino vem do <see cref="IWhatsAppOptionsResolver"/> (banco →
/// env) — o Grupo escolhido na UI é refletido por delivery, sem restart.
/// Nenhum dos dois = Failed com erro pt-BR apontando a seção.
/// </summary>
public sealed class NotificationWhatsAppWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationWhatsAppWorker> logger)
    : NotificationChannelWorkerBase(scopeFactory, logger, NotificationChannel.WhatsApp)
{
    protected override async Task DispatchAsync(
        PendingDeliveryItem item,
        NotificationDelivery delivery,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // ADR-062: options efetivas (banco → env) por dispatch — resolvido no
        // scope do delivery (o worker é singleton; o resolver é scoped).
        var whatsapp = await services.GetRequiredService<IWhatsAppOptionsResolver>()
            .ResolveAsync(cancellationToken);
        var destination = !string.IsNullOrWhiteSpace(whatsapp.GroupId)
            ? whatsapp.GroupId
            : whatsapp.Phone;
        if (string.IsNullOrWhiteSpace(destination))
            throw new InvalidOperationException(
                "Canal WhatsApp sem GroupId nem Phone configurado (Notifications:Channels:WhatsApp).");

        // F33-T005/ADR-059: tenta montar o cartão (série Zabbix + Skia);
        // factory ausente/falha/série vazia = Chart null → sendText puro.
        var chart = await TryCreateChartAsync(item, services, cancellationToken);

        await services.GetRequiredService<IWhatsAppNotificationDispatcher>().SendAsync(
            new WhatsAppNotificationMessage(
                item.DeliveryId, item.NotificationId, item.UserId,
                destination, item.Title, item.Message, item.CreatedAt,
                Chart: chart),
            cancellationToken);
    }

    private async Task<IncidentChartData?> TryCreateChartAsync(
        PendingDeliveryItem item,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var factory = services.GetService<IWhatsAppChartFactory>();
        if (factory is null)
            return null;

        try
        {
            return await factory.TryCreateAsync(
                item.DeviceId, item.Title, item.Message, item.EventType, item.CreatedAt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "WhatsApp {DeliveryId}: sem gráfico (fábrica falhou) — envio em texto.",
                item.DeliveryId);
            return null;
        }
    }
}
