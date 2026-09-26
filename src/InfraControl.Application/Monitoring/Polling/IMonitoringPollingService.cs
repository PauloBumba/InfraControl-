namespace InfraControl.Application.Monitoring.Polling;

/// <summary>
/// Polling periódico de monitoring (F5-T007 / MON-010): executa, a cada
/// ciclo, os checks habilitados de todos os devices com IP. Separado do
/// worker (padrão ICleanupService): o worker só agenda, o serviço tem a
/// regra e é testável sem host.
/// </summary>
public interface IMonitoringPollingService
{
    /// <summary>Executa um ciclo de polling; retorna quantos checks
    /// efetivamente rodaram (devices sem IP, checks desabilitados e
    /// erros por check não contam).</summary>
    Task<int> PollOnceAsync(CancellationToken cancellationToken = default);
}
