using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Identidade externa estável de um Device (F19-T006): 1 Device → N
/// referências (uma por fonte/identificador), unicidade física em
/// (Source, ExternalId). É a ponte "MAC/Device ID → Device" das integrações
/// (UniFi na F20). A resolução do Zabbix por IP/hostname permanece intacta —
/// esta tabela só é consultada quando a fonte manda um identificador
/// estável (regra 6 da revisão F19/F20/F21).
/// </summary>
public sealed class ExternalDeviceReference : Entity
{
    public const int ExternalIdMaxLength = 128;

    public ExternalDeviceSource Source { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public Guid DeviceId { get; private set; }

    private ExternalDeviceReference() { }

    public static ExternalDeviceReference Create(
        ExternalDeviceSource source,
        string externalId,
        Guid deviceId)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        return new ExternalDeviceReference
        {
            Source = source,
            ExternalId = NormalizeExternalId(externalId),
            DeviceId = deviceId
        };
    }

    /// <summary>
    /// O identificador é a chave de lookup — trim + minúsculas (MAC/UUID de
    /// fonte são case-insensitive), mesmo critério das buscas de device.
    /// </summary>
    public static string NormalizeExternalId(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException(
                "O identificador externo é obrigatório.", nameof(externalId));

        var normalized = externalId.Trim().ToLowerInvariant();
        if (normalized.Length > ExternalIdMaxLength)
            throw new ArgumentException(
                $"O identificador externo pode ter no máximo {ExternalIdMaxLength} caracteres.",
                nameof(externalId));

        return normalized;
    }
}
