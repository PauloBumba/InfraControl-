using System.Diagnostics;

namespace InfraControl.Api.Workers;

/// <summary>
/// ActivitySource dos hosted services da API (notificações por canal,
/// health-status). Um span "worker {nome}" por ciclo mantém os commands e
/// queries SQL executados dentro como filhos no Tempo/Grafana
/// (AddSource("InfraControl.*") já escuta este nome).
/// </summary>
public static class ApiWorkerTracing
{
    public static readonly ActivitySource Source = new("InfraControl.Api.Workers");
}
