using System.Diagnostics;

namespace InfraControl.Workers.Backup;

public sealed record ProcessResult(int ExitCode, string StandardError, bool TimedOut = false);

/// <summary>
/// Seam para executar processos externos (F12-T003): o serviço de backup
/// usa a implementação real; os testes usam um falso sem criar processo.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken);
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        foreach (var (key, value) in environment)
            process.StartInfo.Environment[key] = value;

        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // processo pode já ter saído — o cancelamento manda
            }

            throw;
        }

        return new ProcessResult(process.ExitCode, await stderrTask);
    }
}
