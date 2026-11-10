using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Instância de serviço do cluster da plataforma (spec evolução — "o admin
/// pode adicionar réplica ou instância"): registro administrável de cada
/// nó que compõe a plataforma — APIs, frontends, workers, bancos, filas,
/// integrações externas (Zabbix, UniFi, Ollama, Adminer…) e réplicas
/// futuras. O health é verificado sob demanda por HTTP no
/// <see cref="HealthUrl"/> configurado; <see cref="Weight"/> alimenta a
/// visão de distribuição de requisições por papel (<see cref="Kind"/>).
/// </summary>
public sealed class ServiceInstance : Entity
{
    public const int NameMaxLength = 120;
    public const int KindMaxLength = 40;
    public const int HostMaxLength = 255;
    public const int HealthUrlMaxLength = 500;
    public const int NotesMaxLength = 500;

    /// <summary>Papel do nó no cluster: api, frontend, worker, database, cache, queue, integration, custom.</summary>
    public string Kind { get; private set; } = "custom";

    /// <summary>Nome amigável (ex.: "API réplica 2", "Ollama GPU-01").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Host ou endereço interno (ex.: "api-2", "infracontrol-postgres").</summary>
    public string? Host { get; private set; }

    public int? Port { get; private set; }

    /// <summary>URL de health para probe HTTP (ex.: "http://api-2:8080/health/live").</summary>
    public string? HealthUrl { get; private set; }

    /// <summary>Peso relativo na distribuição de requisições deste Kind (0-100).</summary>
    public int Weight { get; private set; } = 1;

    /// <summary>Se false, a instância fica registrada mas fora da distribuição/saúde ativa.</summary>
    public bool IsActive { get; private set; } = true;

    public string? Notes { get; private set; }

    /// <summary>Último resultado do probe: "up", "down" ou null (nunca verificado).</summary>
    public string? LastStatus { get; private set; }

    public DateTime? LastCheckedAt { get; private set; }

    private ServiceInstance() { }

    public static ServiceInstance Create(
        string kind, string name, string? host, int? port,
        string? healthUrl, int weight, bool isActive,
        string? notes = null)
    {
        var instance = new ServiceInstance();
        instance.Apply(kind, name, host, port, healthUrl, weight, isActive, notes);
        return instance;
    }

    public void Update(
        string kind, string name, string? host, int? port,
        string? healthUrl, int weight, bool isActive,
        string? notes = null) =>
        Apply(kind, name, host, port, healthUrl, weight, isActive, notes);

    /// <summary>Registra o resultado de um probe de saúde.</summary>
    public void RecordHealthCheck(string status)
    {
        LastStatus = status is "up" or "down" ? status : "unknown";
        LastCheckedAt = DateTime.UtcNow;
    }

    private void Apply(
        string kind, string name, string? host, int? port,
        string? healthUrl, int weight, bool isActive,
        string? notes)
    {
        var normalizedKind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedKind.Length == 0 || normalizedKind.Length > KindMaxLength)
            throw new ArgumentException(
                $"O tipo da instância deve ter entre 1 e {KindMaxLength} caracteres.",
                nameof(kind));

        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length == 0 || normalizedName.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da instância deve ter entre 1 e {NameMaxLength} caracteres.",
                nameof(name));

        if (port is < 1 or > 65535)
            throw new ArgumentException("A porta deve estar entre 1 e 65535.", nameof(port));

        if (weight is < 0 or > 100)
            throw new ArgumentException("O peso deve estar entre 0 e 100.", nameof(weight));

        Kind = normalizedKind;
        Name = normalizedName;
        Host = NormalizeOptional(host, HostMaxLength, nameof(host));
        Port = port;
        HealthUrl = NormalizeOptional(healthUrl, HealthUrlMaxLength, nameof(healthUrl));
        if (HealthUrl is not null &&
            !HealthUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !HealthUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "A URL de health deve começar com http:// ou https://.", nameof(healthUrl));
        Weight = weight;
        IsActive = isActive;
        Notes = NormalizeOptional(notes, NotesMaxLength, nameof(notes));
    }

    private static string? NormalizeOptional(string? value, int max, string param)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new ArgumentException($"Valor excede {max} caracteres.", param);
        return trimmed;
    }
}
