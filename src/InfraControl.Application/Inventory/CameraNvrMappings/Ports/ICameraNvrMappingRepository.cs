using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Inventory.CameraNvrMappings.Ports;

public interface ICameraNvrMappingRepository : IRepository<CameraNvrMapping>
{
    /// <summary>Mapeamento de um canal específico em um NVR (pré-check de unicidade, F30-T005c).</summary>
    Task<CameraNvrMapping?> GetByNvrAndChannelAsync(
        Guid nvrId, int channelNumber, CancellationToken cancellationToken = default);

    Task<PagedResult<CameraNvrMapping>> SearchAsync(
        Guid? nvrId,
        Guid? cameraId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
