using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F55-T003: estado de "consumo pausado" de uma fila do RabbitMQ.
/// O pause é broker-level (consumers cancelados via Management API); para o
/// resume reciclarmos as conexões certas, os nomes são persistidos aqui —
/// sem isso um restart da API perderia o mapa fila→conexão.
/// </summary>
public sealed class MessagingQueuePause : Entity
{
    public const int QueueNameMaxLength = 255;
    public const int VhostMaxLength = 255;

    public string QueueName { get; private set; } = string.Empty;
    public string Vhost { get; private set; } = "/";

    /// <summary>Nomes das conexões (Management API) que tinham consumers na fila no pause.</summary>
    public string ConnectionNames { get; private set; } = string.Empty;

    public DateTime PausedAtUtc { get; private set; }
    public string? PausedByUserId { get; private set; }
    public string? Reason { get; private set; }

    private MessagingQueuePause() { }

    public static MessagingQueuePause Create(
        string queueName, string vhost, IEnumerable<string> connectionNames,
        string? pausedByUserId, string? reason)
    {
        var pause = new MessagingQueuePause
        {
            QueueName = queueName.Trim(),
            Vhost = string.IsNullOrWhiteSpace(vhost) ? "/" : vhost.Trim(),
            ConnectionNames = string.Join('\n', connectionNames.Where(c => !string.IsNullOrWhiteSpace(c))),
            PausedAtUtc = DateTime.UtcNow,
            PausedByUserId = pausedByUserId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        };
        return pause;
    }

    public IReadOnlyList<string> GetConnectionNames()
        => ConnectionNames.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    public void Touch() => base.Touch();
}
