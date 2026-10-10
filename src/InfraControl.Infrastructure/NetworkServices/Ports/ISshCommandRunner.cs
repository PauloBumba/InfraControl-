namespace InfraControl.Infrastructure.NetworkServices.Ports;

/// <summary>
/// Seam de execução SSH de leitura (F18-T005 · ADR-042 §3): o alvo e a
/// credencial vêm da config (NetworkServices:*); falha de conexão vira
/// <see cref="InvalidOperationException"/> com motivo pt-BR (→ 503 na
/// API), sem expor a senha.
/// </summary>
public interface ISshCommandRunner
{
    Task<string> ExecuteAsync(
        string command, CancellationToken cancellationToken = default);
}
