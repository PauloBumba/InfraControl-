using InfraControl.Workers.Backup;

namespace InfraControl.IntegrationTests.Backup;

/// <summary>
/// Fase P / decisão do usuário (2026-09-30: "usa banco com docker"): o
/// round-trip de backup (F12-T004) não depende mais de pg_dump/pg_restore
/// locais no PATH — as ferramentas rodam de dentro da MESMA imagem
/// <c>postgres:17-alpine</c> que o Testcontainers usa, via
/// <c>docker run --rm</c>. Traduções: <c>-h localhost</c> →
/// <c>host.docker.internal</c> (a porta publicada do Testcontainers segue a
/// mesma) e caminhos absolutos Windows de dump → <c>/backup/</c> via bind
/// mount (arquivo aparece no host após o container sair). Fora de
/// pg_dump/pg_restore a falha é explícita; Docker ausente também falha com
/// instrução — nunca skip escondido.
/// </summary>
public sealed class DockerPgToolRunner(string image = "postgres:17-alpine") : IProcessRunner
{
    private readonly SystemProcessRunner _inner = new();

    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        if (fileName is not ("pg_dump" or "pg_restore"))
            throw new InvalidOperationException(
                $"DockerPgToolRunner executa somente pg_dump/pg_restore (recebido '{fileName}').");

        var translated = new List<string>(arguments.Count);
        var mounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var needsHostGateway = false;

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            // localhost/127.0.0.1 do Testcontainers → host visto de dentro
            // do container; a porta publicada no host continua igual.
            if (argument == "-h" && i + 1 < arguments.Count)
            {
                translated.Add("-h");
                translated.Add("host.docker.internal");
                needsHostGateway = true;
                i++;
                continue;
            }

            // -f <dump.dump> (pg_dump) e o positional final (pg_restore):
            // caminho Windows vira /backup/<arquivo> com mount do diretório.
            if (Path.IsPathRooted(argument))
            {
                var directory = Path.GetDirectoryName(argument);
                if (!string.IsNullOrEmpty(directory))
                    mounts.Add(directory);

                translated.Add("/backup/" + Path.GetFileName(argument));
                continue;
            }

            translated.Add(argument);
        }

        var dockerArguments = new List<string> { "run", "--rm" };

        // host.docker.internal só existe de fábrica no Docker Desktop; no
        // Engine Linux (CI/dev) o nome tem de ser apontado para o gateway com
        // host-gateway, senão o pg_dump sai com "could not translate host name".
        if (needsHostGateway)
        {
            dockerArguments.Add("--add-host");
            dockerArguments.Add("host.docker.internal:host-gateway");
        }

        foreach (var directory in mounts)
        {
            dockerArguments.Add("-v");
            dockerArguments.Add($"{directory}:/backup");
        }

        foreach (var (key, value) in environment)
        {
            dockerArguments.Add("-e");
            dockerArguments.Add($"{key}={value}");
        }

        dockerArguments.Add(image);
        dockerArguments.Add(fileName);
        dockerArguments.AddRange(translated);

        return await _inner.RunAsync(
            ResolveDockerCli(),
            dockerArguments,
            new Dictionary<string, string>(),
            cancellationToken);
    }

    /// <summary>
    /// <c>docker</c> no PATH (Windows: <c>docker.exe</c>) ou o install
    /// por-usuário do Docker Desktop (não fica no PATH de sessões iniciadas
    /// antes da instalação). O nome era só <c>docker.exe</c>, então em
    /// Linux/macOS o teste falhava com "docker CLI não encontrado" mesmo com
    /// o Docker instalado.
    /// </summary>
    private static string ResolveDockerCli()
    {
        var executable = OperatingSystem.IsWindows() ? "docker.exe" : "docker";
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(entry.Trim(), executable);
            if (File.Exists(candidate))
                return candidate;
        }

        if (OperatingSystem.IsWindows())
        {
            var userInstall = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "DockerDesktop", "resources", "bin", "docker.exe");
            if (File.Exists(userInstall))
                return userInstall;
        }

        throw new InvalidOperationException(
            $"CLI do Docker ('{executable}') não encontrada (nem no PATH nem no install " +
            "por-usuário do Docker Desktop) — o round-trip de backup/restore precisa " +
            "do Docker nesta máquina.");
    }
}
