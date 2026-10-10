namespace InfraControl.Application.AI.Context;

/// <summary>Um chunk de artigo (posição + texto).</summary>
public sealed record KnowledgeChunk(int Index, string Text);

/// <summary>
/// Chunking de artigos de Knowledge para ingesta vetorial
/// (FAI-T009/S8 · ADR-038 §3): por PARÁGRAFO com limite documentado —
/// <c>MaxChunkCharacters</c> = 1500 (modelo local 1.5B tem janela
/// curta, §11); parágrafo maior que o limite é partido em blocos
/// cortados no último espaço antes do limite. Puro e determinístico —
/// sem dependência de provider/store (testável sem Docker).
/// </summary>
public static class KnowledgeChunker
{
    public const int MaxChunkCharacters = 1500;

    public static IReadOnlyList<KnowledgeChunk> Split(string? content, int maxCharacters = MaxChunkCharacters)
    {
        if (maxCharacters < 100)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters), "Limite mínimo de 100 caracteres por chunk.");

        if (string.IsNullOrWhiteSpace(content))
            return [];

        var chunks = new List<KnowledgeChunk>();
        foreach (var paragraph in content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (paragraph.Length <= maxCharacters)
            {
                chunks.Add(new KnowledgeChunk(chunks.Count, paragraph));
                continue;
            }

            var remaining = paragraph;
            while (remaining.Length > 0)
            {
                var size = Math.Min(remaining.Length, maxCharacters);
                if (size < remaining.Length)
                {
                    var lastSpace = remaining.LastIndexOf(' ', size);
                    if (lastSpace > maxCharacters / 2)
                        size = lastSpace;
                }

                chunks.Add(new KnowledgeChunk(chunks.Count, remaining[..size].Trim()));
                remaining = remaining[size..].TrimStart();
            }
        }

        return chunks;
    }
}
