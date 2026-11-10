namespace InfraControl.Application.Operations.Cluster.Ports;

/// <summary>Probe HTTP de health de uma instância do cluster.</summary>
public interface IServiceHealthProbe
{
    /// <summary>GET na URL com timeout curto; true = resposta 2xx/3xx.</summary>
    Task<bool> IsHealthyAsync(string healthUrl, CancellationToken cancellationToken = default);
}
