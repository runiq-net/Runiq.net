using System.Diagnostics;

namespace Runiq.AI.Cli.Infrastructure;

/// <summary>Runs child processes while draining both redirected output streams concurrently.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(10);

    public ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        try { process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Process '{fileName}' exceeded the {ProcessTimeout.TotalMinutes:0}-minute execution limit.");
        }
        Task.WhenAll(standardOutputTask, standardErrorTask).GetAwaiter().GetResult();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = standardOutputTask.Result,
            StandardError = standardErrorTask.Result
        };
    }
}

