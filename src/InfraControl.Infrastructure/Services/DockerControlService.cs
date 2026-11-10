using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfraControl.Application.Operations.Cluster.Ports;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Docker Engine API via socket unix (/var/run/docker.sock). HTTP/1.1 sobre
/// UDS com SocketsHttpHandler.ConnectCallback — sem dependência extra.
/// ListContainers usa /containers/json filtrado pelo projeto Compose;
/// stats/inspect são one-shot por container; logs usam demultiplex do
/// stream binário do Engine.
/// </summary>
public sealed class DockerControlService(IHttpClientFactory httpClientFactory)
    : IDockerControl
{
    public const string HttpClientName = "docker-engine";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<DockerContainerDto>?> ListContainersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync("/containers/json?all=1", cancellationToken);
            var raw = JsonSerializer.Deserialize<List<DockerContainerJson>>(json, JsonOptions);
            // Só containers DESTE projeto Compose — o socket enxerga o host
            // inteiro e a tela do cluster não pode listar containers alheios.
            var project = Environment.GetEnvironmentVariable("COMPOSE_PROJECT_NAME") ?? "infracontrol";
            return raw?.Where(c => BelongsToProject(c, project)).Select(Map).ToArray() ?? [];
        }
        catch
        {
            // Socket ausente/sem permissão = Docker indisponível para a API.
            return null;
        }
    }

    public async Task<DockerContainerStats?> GetContainerStatsAsync(
        string containerIdOrName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var id = Uri.EscapeDataString(containerIdOrName.TrimStart('/'));
            // stream=false + one-shot: o Engine devolve um único JSON — sem
            // manter conexão de stream aberta por container.
            var json = await client.GetStringAsync(
                $"/containers/{id}/stats?stream=false&one-shot=true", cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double? cpuPercent = null;
            var cpu = root.TryGetProperty("cpu_stats", out var cs) ? cs : default;
            var precpu = root.TryGetProperty("precpu_stats", out var pcs) ? pcs : default;
            if (cpu.ValueKind == JsonValueKind.Object && precpu.ValueKind == JsonValueKind.Object)
            {
                var cpuDelta = GetLong(cpu, "cpu_usage", "total_usage")
                    - GetLong(precpu, "cpu_usage", "total_usage");
                var systemDelta = GetLong(cpu, "system_cpu_usage")
                    - GetLong(precpu, "system_cpu_usage");
                var onlineCpus = GetLong(cpu, "online_cpus") is > 0 and var n ? n : 1;
                if (systemDelta > 0 && cpuDelta >= 0)
                    cpuPercent = Math.Round(cpuDelta * 100.0 / systemDelta * onlineCpus, 2);
            }

            long? memory = null;
            long? memoryLimit = null;
            double? memoryPercent = null;
            if (root.TryGetProperty("memory_stats", out var mem)
                && mem.ValueKind == JsonValueKind.Object)
            {
                if (mem.TryGetProperty("usage", out var usage)
                    && usage.ValueKind == JsonValueKind.Number)
                {
                    memory = usage.GetInt64();
                }
                if (mem.TryGetProperty("limit", out var limitEl)
                    && limitEl.ValueKind == JsonValueKind.Number)
                {
                    memoryLimit = limitEl.GetInt64();
                    // Cgroup v2 reporta limite "infinito" (~2^63); trata como sem limite.
                    if (memoryLimit > 0 && memoryLimit < long.MaxValue / 2 && memory is > 0)
                        memoryPercent = Math.Round(memory.Value * 100.0 / memoryLimit.Value, 2);
                    else
                        memoryLimit = null;
                }
            }

            long rx = 0, tx = 0;
            if (root.TryGetProperty("networks", out var nets)
                && nets.ValueKind == JsonValueKind.Object)
            {
                foreach (var net in nets.EnumerateObject())
                {
                    rx += GetLong(net.Value, "rx_bytes");
                    tx += GetLong(net.Value, "tx_bytes");
                }
            }

            long blkRead = 0, blkWrite = 0;
            if (root.TryGetProperty("blkio_stats", out var blkio)
                && blkio.TryGetProperty("io_service_bytes_recursive", out var io)
                && io.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in io.EnumerateArray())
                {
                    var op = entry.TryGetProperty("op", out var o) ? o.GetString() : null;
                    var value = GetLong(entry, "value");
                    if (string.Equals(op, "read", StringComparison.OrdinalIgnoreCase)) blkRead += value;
                    else if (string.Equals(op, "write", StringComparison.OrdinalIgnoreCase)) blkWrite += value;
                }
            }

            long? pids = null;
            if (root.TryGetProperty("pids_stats", out var pidsStats)
                && pidsStats.TryGetProperty("current", out var cur)
                && cur.ValueKind == JsonValueKind.Number)
            {
                pids = cur.GetInt64();
            }

            if (cpuPercent is null && memory is null) return null;
            return new DockerContainerStats(
                cpuPercent, memory, memoryLimit, memoryPercent,
                rx > 0 ? rx : null, tx > 0 ? tx : null,
                blkRead > 0 ? blkRead : null, blkWrite > 0 ? blkWrite : null,
                pids);
        }
        catch
        {
            return null;
        }
    }

    public async Task<DockerContainerInspectDto?> GetContainerInspectAsync(
        string containerIdOrName,
        CancellationToken cancellationToken = default)
    {
        var inspect = await GetInspectJsonAsync(containerIdOrName, cancellationToken);
        if (inspect is null) return null;
        var root = inspect.RootElement;
        var state = root.TryGetProperty("State", out var s) ? s : default;
        var startedAt = state.ValueKind == JsonValueKind.Object
            && state.TryGetProperty("StartedAt", out var sa)
            && sa.ValueKind == JsonValueKind.String
                ? sa.GetString()
                : null;
        var restarts = root.TryGetProperty("RestartCount", out var rc)
            && rc.ValueKind == JsonValueKind.Number ? rc.GetInt64() : 0;
        string? health = null;
        if (state.ValueKind == JsonValueKind.Object
            && state.TryGetProperty("Health", out var h)
            && h.ValueKind == JsonValueKind.Object
            && h.TryGetProperty("Status", out var hs)
            && hs.ValueKind == JsonValueKind.String)
        {
            health = hs.GetString();
        }

        // Limites efetivos do HostConfig (0/ausente = sem limite).
        double? cpuLimit = null;
        long? memoryLimit = null;
        if (root.TryGetProperty("HostConfig", out var hostConfig)
            && hostConfig.ValueKind == JsonValueKind.Object)
        {
            if (hostConfig.TryGetProperty("NanoCpus", out var nano)
                && nano.ValueKind == JsonValueKind.Number
                && nano.GetInt64() > 0)
            {
                cpuLimit = nano.GetInt64() / 1_000_000_000.0;
            }
            if (hostConfig.TryGetProperty("Memory", out var memLimit)
                && memLimit.ValueKind == JsonValueKind.Number
                && memLimit.GetInt64() > 0)
            {
                memoryLimit = memLimit.GetInt64();
            }
        }
        return new DockerContainerInspectDto(startedAt, restarts, health, cpuLimit, memoryLimit);
    }

    public async Task<DockerTopologyDto?> GetTopologyAsync(
        CancellationToken cancellationToken = default)
    {
        var containers = await ListContainersAsync(cancellationToken);
        if (containers is null) return null;

        // Inspect de cada container do projeto — em paralelo, tolerante a falha.
        var details = await Task.WhenAll(containers.Select(async c =>
            (c, inspect: await GetInspectJsonAsync(c.Id, cancellationToken))));

        var nodes = new List<DockerTopologyNodeDto>();
        var networkMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (container, inspect) in details)
        {
            if (inspect is null) continue;
            var root = inspect.RootElement;

            var service = container.Name;
            var networks = new List<string>();
            var ports = new List<string>();
            string? health = null;
            long restarts = 0;

            if (root.TryGetProperty("Config", out var cfg)
                && cfg.TryGetProperty("Labels", out var labels)
                && labels.ValueKind == JsonValueKind.Object
                && labels.TryGetProperty("com.docker.compose.service", out var svc)
                && svc.ValueKind == JsonValueKind.String)
            {
                service = svc.GetString() ?? service;
            }

            if (root.TryGetProperty("NetworkSettings", out var netSettings))
            {
                if (netSettings.TryGetProperty("Networks", out var nets)
                    && nets.ValueKind == JsonValueKind.Object)
                {
                    foreach (var net in nets.EnumerateObject())
                    {
                        networks.Add(net.Name);
                        if (!networkMap.TryGetValue(net.Name, out var members))
                            networkMap[net.Name] = members = [];
                        members.Add(container.Name);
                    }
                }
                if (netSettings.TryGetProperty("Ports", out var exposed)
                    && exposed.ValueKind == JsonValueKind.Object)
                {
                    foreach (var port in exposed.EnumerateObject())
                    {
                        ports.Add(port.Name);
                        // Porta publicada (host→container) quando houver binding.
                        if (port.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var binding in port.Value.EnumerateArray())
                            {
                                if (binding.TryGetProperty("HostPort", out var hp)
                                    && hp.ValueKind == JsonValueKind.String)
                                {
                                    ports.Add($"{hp.GetString()}→{port.Name}");
                                }
                            }
                        }
                    }
                }
            }

            if (root.TryGetProperty("State", out var st) && st.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("RestartCount", out var rc)
                    && rc.ValueKind == JsonValueKind.Number)
                    restarts = rc.GetInt64();
                if (st.TryGetProperty("Health", out var h)
                    && h.ValueKind == JsonValueKind.Object
                    && h.TryGetProperty("Status", out var hs)
                    && hs.ValueKind == JsonValueKind.String)
                    health = hs.GetString();
            }

            nodes.Add(new DockerTopologyNodeDto(
                container.Id, container.Name, service, container.Image,
                container.State, networks, ports.Distinct().ToArray(), health, restarts));
        }

        var networksDto = networkMap
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new DockerNetworkDto(kv.Key, kv.Value.OrderBy(n => n).ToArray()))
            .ToArray();

        return new DockerTopologyDto(nodes.OrderBy(n => n.Service).ToArray(), networksDto);
    }

    /// <summary>Inspect completo de um container (JsonDocument do caller dispõe).</summary>
    private async Task<JsonDocument?> GetInspectJsonAsync(
        string containerIdOrName,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var id = Uri.EscapeDataString(containerIdOrName.TrimStart('/'));
            var json = await client.GetStringAsync($"/containers/{id}/json", cancellationToken);
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static long GetLong(JsonElement parent, params string[] path)
    {
        var el = parent;
        foreach (var key in path)
        {
            if (el.ValueKind != JsonValueKind.Object
                || !el.TryGetProperty(key, out el)
                || el.ValueKind != JsonValueKind.Number)
                return 0;
        }
        return el.GetInt64();
    }

    public async Task<string?> GetContainerLogsAsync(
        string containerIdOrName,
        int tailLines,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var id = Uri.EscapeDataString(containerIdOrName.TrimStart('/'));
            var tail = Math.Clamp(tailLines, 1, 2000);
            // stdout+stderr combinados; timestamps do Docker desligados.
            using var response = await client.GetAsync(
                $"/containers/{id}/logs?stdout=1&stderr=1&tail={tail}",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            var raw = ms.ToArray();

            // Containers sem TTY devolvem stream multiplexado: header de 8
            // bytes por frame [stream(1) 0 0 0 size(4 BE)]. Desembrulha;
            // payload já é texto UTF-8.
            var text = Demultiplex(raw);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return string.Join('\n', lines.TakeLast(tail));
        }
        catch
        {
            return null;
        }
    }

    private static string Demultiplex(byte[] raw)
    {
        // Heurística: se não houver header válido nos primeiros bytes, o
        // container roda com TTY e o payload já vem puro.
        if (raw.Length < 8
            || (raw[0] != 1 && raw[0] != 2)
            || raw[1] != 0 || raw[2] != 0 || raw[3] != 0)
            return System.Text.Encoding.UTF8.GetString(raw);

        using var ms = new MemoryStream();
        var offset = 0;
        while (offset + 8 <= raw.Length)
        {
            var size = (raw[offset + 4] << 24) | (raw[offset + 5] << 16)
                | (raw[offset + 6] << 8) | raw[offset + 7];
            if (size < 0 || offset + 8 + size > raw.Length) break;
            ms.Write(raw, offset + 8, size);
            offset += 8 + size;
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    public async Task<bool> ControlAsync(
        string containerIdOrName,
        DockerContainerAction action,
        CancellationToken cancellationToken = default)
    {
        var verb = action switch
        {
            DockerContainerAction.Start => "start",
            DockerContainerAction.Stop => "stop",
            DockerContainerAction.Restart => "restart",
            _ => null,
        };
        if (verb is null) return false;

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var id = Uri.EscapeDataString(containerIdOrName.TrimStart('/'));
            var response = await client.PostAsync($"/containers/{id}/{verb}", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DockerContainerUpdateStatus> RemoveContainerAsync(
        string containerIdOrName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var id = Uri.EscapeDataString(containerIdOrName.TrimStart('/'));
            // Sem force: container em execução → 409 → Failed (a UI orienta
            // parar antes); inexistente → 404 → NotFound.
            var response = await client.DeleteAsync($"/containers/{id}", cancellationToken);
            if (response.IsSuccessStatusCode) return DockerContainerUpdateStatus.Done;
            return response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? DockerContainerUpdateStatus.NotFound
                : DockerContainerUpdateStatus.Failed;
        }
        catch
        {
            return DockerContainerUpdateStatus.Failed;
        }
    }

    public async Task<DockerContainerUpdateStatus> UpdateResourceLimitsAsync(
        string containerIdOrName,
        long? memoryBytes,
        double? cpuCores,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var id = Uri.EscapeDataString(containerIdOrName.TrimStart('/'));

            // Merge com os limites atuais: o update substitui o HostConfig
            // inteiro — mandar 0 no campo não informado APAGARIA o limite
            // existente (ex.: só memória nova não pode zerar o limite de CPU).
            var inspect = await GetInspectJsonAsync(containerIdOrName, cancellationToken);
            if (inspect is null) return DockerContainerUpdateStatus.NotFound;

            long currentMemory = 0;
            long currentNanoCpus = 0;
            if (inspect.RootElement.TryGetProperty("HostConfig", out var hostConfig)
                && hostConfig.ValueKind == JsonValueKind.Object)
            {
                if (hostConfig.TryGetProperty("Memory", out var m)
                    && m.ValueKind == JsonValueKind.Number)
                    currentMemory = Math.Max(0, m.GetInt64());
                if (hostConfig.TryGetProperty("NanoCpus", out var n)
                    && n.ValueKind == JsonValueKind.Number)
                    currentNanoCpus = Math.Max(0, n.GetInt64());
            }

            var payload = new
            {
                HostConfig = new
                {
                    Memory = memoryBytes ?? currentMemory,
                    NanoCpus = cpuCores is { } cores
                        ? (long)(cores * 1_000_000_000.0)
                        : currentNanoCpus,
                },
            };

            using var response = await client.PostAsJsonAsync(
                $"/containers/{id}/update", payload, cancellationToken);
            return response.IsSuccessStatusCode
                ? DockerContainerUpdateStatus.Done
                : DockerContainerUpdateStatus.Failed;
        }
        catch
        {
            return DockerContainerUpdateStatus.Failed;
        }
    }

    private static bool BelongsToProject(DockerContainerJson c, string project)
    {
        // Label do Compose é a fonte mais confiável. Normaliza '-' final:
        // o projeto real é "infracontrol-" e o fallback do env é "infracontrol".
        if (c.Labels is not null
            && c.Labels.TryGetValue("com.docker.compose.project", out var p)
            && string.Equals(p.TrimEnd('-'), project.TrimEnd('-'), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fallback por nome: exige separador após o prefixo para não casar
        // projetos irmãos — "infracontrolk6-*" não pode entrar em "infracontrol".
        var name = c.Names is { Length: > 0 } ? c.Names[0].TrimStart('/') : c.Id;
        return name.Length > project.Length
            && name.StartsWith(project, StringComparison.OrdinalIgnoreCase)
            && name[project.Length] is '-' or '/' or '.';
    }

    private static DockerContainerDto Map(DockerContainerJson c)
    {
        var names = c.Names ?? [];
        var name = names.Length > 0 ? names[0].TrimStart('/') : c.Id[..Math.Min(12, c.Id.Length)];
        return new DockerContainerDto(
            c.Id,
            name,
            c.Image ?? string.Empty,
            c.State ?? "unknown",
            c.Status ?? string.Empty,
            null,
            null);
    }

    private sealed class DockerContainerJson
    {
        public string Id { get; set; } = string.Empty;
        public string[]? Names { get; set; }
        public string? Image { get; set; }
        public string? State { get; set; }
        public string? Status { get; set; }
        public Dictionary<string, string>? Labels { get; set; }
    }
}
