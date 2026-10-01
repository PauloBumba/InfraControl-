namespace InfraControl.Application.Notifications.Feed;

/// <summary>
/// Item do feed (F18-T001): `{id, eventType, title, message, createdAt}`
/// — `createdAt` = `CreateDateTime` da entidade. **Sem estado de
/// leitura**: `Notification` não tem flag de lida e a task autorizou
/// entregar sem "não lidas" (evita migração de schema; `ReadAt` +
/// `POST {id}/read` ficam como extensão futura se o produto pedir).
/// </summary>
public sealed record NotificationFeedItemDto(
    Guid Id,
    string EventType,
    string Title,
    string Message,
    DateTime CreatedAt);
