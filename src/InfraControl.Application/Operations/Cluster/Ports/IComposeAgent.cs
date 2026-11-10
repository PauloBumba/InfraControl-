namespace InfraControl.Application.Operations.Cluster.Ports;

/// <summary>Container de um serviço Compose visto pelo agente.</summary>
public sealed record ComposeServiceContainerDto(
    string Name,
    string State,
    string Status);

/// <summary>
/// Serviço Compose do projeto com suas réplicas reais (labels do Engine,
/// não o cadastro manual de ServiceInstance).
/// </summary>
public sealed record ComposeServiceDto(
    string Service,
    int Total,
    int Running,
    IReadOnlyList<ComposeServiceContainerDto> Containers);

/// <summary>Resultado de uma operação de escala no agente.</summary>
public sealed record ComposeScaleResult(
    string Service,
    int Replicas,
    bool Success,
    string Output);

/// <summary>
/// Agente privilegiado de escala Compose (docs/65 Fase 3, Decisão C):
/// container dedicado na rede interna que executa
/// `docker compose up -d --no-deps --scale svc=N svc`. A API fala HTTP
/// com ele; a implementação fica na Infrastructure.
/// </summary>
public interface IComposeAgent
{
    /// <summary>
    /// Serviços Compose do projeto com containers agrupados por serviço.
    /// Null = agente indisponível (container fora do ar, token divergente).
    /// </summary>
    Task<IReadOnlyList<ComposeServiceDto>?> ListServicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Escala um serviço para N réplicas. Null = agente indisponível;
    /// Success=false = compose recusou (saída em Output).
    /// </summary>
    Task<ComposeScaleResult?> ScaleAsync(
        string service,
        int replicas,
        CancellationToken cancellationToken = default);
}
