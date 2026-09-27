using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Achado de discovery vinculado a uma execução (docs/03, F4-T002).
/// Modelo genérico aprovado: Kind (protocolo), Summary (legível ≤500) e
/// Values (JSON com os detalhes do provider) — os providers T003–T009
/// gravam no formato deles sem nova migração. DeviceId? opcional liga o
/// achado ao device conhecido (T010 New Device Detection). Escopo da
/// task: somente leitura — a escrita vem com os providers. Cascata puro
/// com o run: excluir run/device exclui os achados.
/// </summary>
public sealed class DiscoveryResult : AggregateRoot
{
    public const int SummaryMaxLength = 500;

    public Guid DiscoveryRunId { get; private set; }
    public DiscoveryResultKind Kind { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public IReadOnlyDictionary<string, string>? Values { get; private set; }
    public Guid? DeviceId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private DiscoveryResult() { }

    public static DiscoveryResult Create(
        Guid discoveryRunId,
        DiscoveryResultKind kind,
        string summary,
        IReadOnlyDictionary<string, string>? values = null,
        Guid? deviceId = null,
        DateTime? createdAt = null)
    {
        if (discoveryRunId == Guid.Empty)
            throw new ArgumentException(
                "O id da execução de discovery é obrigatório.", nameof(discoveryRunId));

        if (!Enum.IsDefined(kind))
            throw new ArgumentException("O tipo do resultado é inválido.", nameof(kind));

        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("O resumo do resultado é obrigatório.", nameof(summary));

        summary = summary.Trim();
        if (summary.Length > SummaryMaxLength)
            throw new ArgumentException(
                $"O resumo do resultado pode ter no máximo {SummaryMaxLength} caracteres.",
                nameof(summary));

        return new DiscoveryResult
        {
            DiscoveryRunId = discoveryRunId,
            Kind = kind,
            Summary = summary,
            Values = values is null ? null : new Dictionary<string, string>(values),
            DeviceId = deviceId,
            CreatedAt = createdAt ?? AppTimeProvider.Current.GetUtcNow().UtcDateTime
        };
    }
}
