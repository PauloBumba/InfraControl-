using System.Text.Json;
using InfraControl.Application.AI.Context;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Vector store pgvector no PostgreSQL existente (FAI-T009/S8 · ADR-038
/// §1/§4): tabela <c>ai_vector_documents</c> com coluna
/// <c>vector(768)</c> (nomic-embed-text) e similaridade de cosseno
/// (<c>&lt;=&gt;</c>). Upsert por id de chunk; busca por cosseno +
/// filtros de metadados jsonb. Requer a extensão <c>vector</c>
/// (imagem <c>pgvector/pgvector:pg17</c>).
/// </summary>
public sealed class PgVectorStore(
    AppDbContext db,
    ILogger<PgVectorStore> logger) : IVectorStore
{
    public async Task UpsertAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        var metadata = JsonSerializer.Serialize(document.Metadata ?? new Dictionary<string, string>());
        var conn = await OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ai_vector_documents (id, text, embedding, metadata)
            VALUES (@id, @text, @embedding::vector, @metadata::jsonb)
            ON CONFLICT (id) DO UPDATE
            SET text = EXCLUDED.text,
                embedding = EXCLUDED.embedding,
                metadata = EXCLUDED.metadata
            """;
        cmd.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Varchar) { Value = document.Id });
        cmd.Parameters.Add(new NpgsqlParameter("@text", NpgsqlDbType.Text) { Value = document.Text });
        cmd.Parameters.Add(new NpgsqlParameter("@embedding", NpgsqlDbType.Text) { Value = ToVectorLiteral(document.Embedding) });
        cmd.Parameters.Add(new NpgsqlParameter("@metadata", NpgsqlDbType.Text) { Value = metadata });
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        logger.LogDebug("Chunk {ChunkId} upsertado no vector store.", document.Id);
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(
        VectorQuery query, CancellationToken cancellationToken = default)
    {
        var conn = await OpenAsync(cancellationToken);
        var limit = Math.Max(1, query.Limit);

        // Distância de cosseno; score = similaridade (1 - distância).
        var sql = new System.Text.StringBuilder(
            "SELECT id, text, metadata, 1 - (embedding <=> @embedding::vector) AS score " +
            "FROM ai_vector_documents WHERE 1 = 1");
        var parameters = new List<NpgsqlParameter>
        {
            new("@embedding", NpgsqlDbType.Text) { Value = ToVectorLiteral(query.Embedding) },
        };

        var index = 0;
        if (query.Filters is { Count: > 0 })
        {
            foreach (var (key, value) in query.Filters)
            {
                var keyParam = $"@fk{index}";
                var valParam = $"@fv{index}";
                index++;
                sql.Append($" AND metadata ->> {keyParam} = {valParam}");
                parameters.Add(new NpgsqlParameter(keyParam, NpgsqlDbType.Text) { Value = key });
                parameters.Add(new NpgsqlParameter(valParam, NpgsqlDbType.Text) { Value = value });
            }
        }

        sql.Append($" ORDER BY embedding <=> @embedding::vector LIMIT @lim");
        parameters.Add(new NpgsqlParameter("@lim", NpgsqlDbType.Integer) { Value = limit });

        var matches = new List<VectorMatch>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql.ToString();
        foreach (var p in parameters) cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(new VectorMatch(
                Id: reader.GetString(0),
                Score: reader.GetDouble(3),
                Text: reader.IsDBNull(1) ? null : reader.GetString(1),
                Metadata: ParseMetadata(reader.IsDBNull(2) ? null : reader.GetString(2))));
        }

        return matches;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);
        return conn;
    }

    private static string ToVectorLiteral(float[] embedding)
        => "[" + string.Join(',', embedding.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

    private static IReadOnlyDictionary<string, string>? ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
