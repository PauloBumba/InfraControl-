using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Histórico de um poll executado (F5-T002) — uma linha por tentativa, com
/// duração, erro e valores (ex.: nível de toner). FK para Device (cascade),
/// <c>Kind</c> solto: o histórico sobrevive a mudanças de perfil do device
/// (remover um check não apaga o passado). <c>CheckedAt</c> = CreateDateTime.
/// </summary>
public sealed class MonitoringResult : AggregateRoot
{
    public const int ErrorMaxLength = 500;

    public Guid DeviceId { get; private set; }
    public CheckKind Kind { get; private set; }
    public bool Succeeded { get; private set; }
    public long DurationMs { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyDictionary<string, string>? Values { get; private set; }

    private MonitoringResult() { }

    public static MonitoringResult Create(
        Guid deviceId,
        CheckKind kind,
        bool succeeded,
        long durationMs,
        string? error = null,
        IReadOnlyDictionary<string, string>? values = null)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        if (durationMs < 0)
            throw new ArgumentException(
                "A duração do poll não pode ser negativa.", nameof(durationMs));

        return new MonitoringResult
        {
            DeviceId = deviceId,
            Kind = kind,
            Succeeded = succeeded,
            DurationMs = durationMs,
            Error = NormalizeError(error),
            Values = values
        };
    }

    private static string? NormalizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;

        var trimmed = error.Trim();
        return trimmed.Length > ErrorMaxLength
            ? trimmed[..ErrorMaxLength]
            : trimmed;
    }
}
