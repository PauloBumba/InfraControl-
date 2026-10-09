namespace InfraControl.Application.AI.Tools;

/// <summary>
/// Registro de tools de IA (FAI-T008/S7 · ADR-039 §20): espelho do
/// <c>IAiProviderRegistry</c> — o servidor MCP futuro expõe o que
/// estiver aqui, e só o que estiver aqui.
/// </summary>
public interface IAiToolRegistry
{
    IAiTool Get(string toolName);
    IReadOnlyCollection<IAiTool> List();
}

/// <summary>
/// Registry em memória montado a partir das tools registradas no DI.
/// Nome case-insensitive (mesmo padrão do ConfigurationProviderRegistry).
/// </summary>
public sealed class InMemoryToolRegistry(IEnumerable<IAiTool> tools) : IAiToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools = tools.ToDictionary(
        tool => tool.Descriptor.Name,
        StringComparer.OrdinalIgnoreCase);

    public IAiTool Get(string toolName) =>
        _tools.TryGetValue(toolName, out var tool)
            ? tool
            : throw new AiToolNotFoundException(toolName);

    public IReadOnlyCollection<IAiTool> List() => [.. _tools.Values];
}
