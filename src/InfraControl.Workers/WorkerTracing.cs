using System.Diagnostics;

namespace InfraControl.Workers;

/// <summary>
/// ActivitySource compartilhado dos hosted services. Cada iteração do loop
/// de um worker abre um span "worker {nome}" — os commands MediatR e as
/// queries SQL executados dentro viram filhos dele no Tempo/Grafana
/// (AddSource("InfraControl.*") já escuta este nome).
/// </summary>
public static class WorkerTracing
{
    public static readonly ActivitySource Source = new("InfraControl.Workers");
}
