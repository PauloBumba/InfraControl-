using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Company.Ports;

/// <summary>
/// Porta do singleton de configuração institucional (spec §3).
/// </summary>
public interface ICompanySettingsRepository : IRepository<CompanySettings>
{
    /// <summary>Retorna a configuração persistida ou null (nunca configurada).</summary>
    Task<CompanySettings?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
