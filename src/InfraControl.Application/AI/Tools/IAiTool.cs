namespace InfraControl.Application.AI.Tools;

/// <summary>
/// Contrato de uma tool de IA (FAI-T008/S7 · docs/34 §20/§21/§52 ·
/// ADR-039): exposição de capacidades de LEITURA sobre Application
/// Services — nunca banco direto (§20). O servidor MCP externo vem
/// depois (ADR-039): aqui está o registry in-process que ele vai
/// servir.
/// </summary>
public interface IAiTool
{
    /// <summary>Contrato estático da tool (§52): nome, descrição, acesso, limites.</summary>
    AiToolDescriptor Descriptor { get; }

    /// <summary>Executa a tool com os argumentos validados pelo executor.</summary>
    Task<AiToolResult> ExecuteAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Descriptor da tool (§52): contrato, autorização, timeout, limite de
/// resultados, política de auditoria e fonte.
/// </summary>
public sealed record AiToolDescriptor(
    string Name,
    string Description,
    AiToolAccess Access,
    TimeSpan Timeout,
    int MaxResults,
    string InputSchema,
    string OutputSchema,
    string AuditPolicy,
    string Source);

/// <summary>Argumentos de execução (chave/valor string — v1 simples e auditável).</summary>
public sealed record AiToolArguments(IReadOnlyDictionary<string, string?> Values)
{
    public static AiToolArguments Empty { get; } =
        new(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

    public string? Get(string name) =>
        Values.TryGetValue(name, out var value) ? value : null;

    public int? GetInt(string name) =>
        int.TryParse(Get(name), out var parsed) ? parsed : null;

    public static AiToolArguments From(params (string Key, string? Value)[] values) =>
        new(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Achado estruturado de leitura (§53 — dados, não texto livre).
/// </summary>
public sealed record AiToolFinding(
    string Id,
    string Title,
    string? Status = null,
    string? Detail = null,
    DateTime? OccurredAt = null);

/// <summary>
/// Resultado no formato da §52 (<c>{findings[], total, source, timestamp}</c>).
/// </summary>
public sealed record AiToolResult(
    string ToolName,
    IReadOnlyList<AiToolFinding> Findings,
    int Total,
    string Source,
    DateTimeOffset TimestampUtc);
