using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F30-T005c (docs/46 §Modelo de domínio): relação câmera ↔ NVR — qual canal
/// do NVR corresponde à DeviceType.Camera. DeviceType.Nvr = 11 (F30-T005a).
/// Canal é 1-based (convenção ONVIF/NVR). Unicidade (nvr_id, channel_number)
/// garantida por pré-check no handler + índice único na migração.
/// </summary>
public sealed class CameraNvrMapping : AggregateRoot
{
    public Guid CameraId { get; private set; }
    public Guid NvrId { get; private set; }
    public int ChannelNumber { get; private set; }

    private CameraNvrMapping() { }

    public static CameraNvrMapping Create(Guid cameraId, Guid nvrId, int channelNumber)
    {
        var mapping = new CameraNvrMapping();
        mapping.Update(cameraId, nvrId, channelNumber);
        mapping.ClearDomainEvents();
        return mapping;
    }

    public void Update(Guid cameraId, Guid nvrId, int channelNumber)
    {
        if (cameraId == Guid.Empty)
            throw new ArgumentException("O id da câmera é obrigatório.", nameof(cameraId));

        if (nvrId == Guid.Empty)
            throw new ArgumentException("O id do NVR é obrigatório.", nameof(nvrId));

        if (channelNumber < 1)
            throw new ArgumentException(
                "O número do canal deve ser maior ou igual a 1.", nameof(channelNumber));

        CameraId = cameraId;
        NvrId = nvrId;
        ChannelNumber = channelNumber;
    }
}
