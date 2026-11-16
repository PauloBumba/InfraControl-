namespace InfraControl.Domain.Enums;

/// <summary>
/// ADR-068: estado do problema lógico de monitoramento (não do evento).
/// <see cref="Alerta"/> = problem aberto sem incidente próprio (Warning ou
/// alerta suprimido por upstream); <see cref="Incidente"/> = problem aberto
/// com incidente próprio; <see cref="Resolvido"/> = ciclo encerrado.
/// "Normal" é a ausência de linha em MonitoringProblemState — não existe
/// estado persistido para problema que nunca abriu.
/// </summary>
public enum ProblemStatus
{
    Alerta = 0,
    Incidente = 1,
    Resolvido = 2
}
