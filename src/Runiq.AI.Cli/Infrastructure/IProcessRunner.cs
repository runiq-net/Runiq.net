namespace Runiq.AI.Cli.Infrastructure;

/// <summary>Abstracts child-process execution for CLI generation operations.</summary>
public interface IProcessRunner
{
    /// <summary>Runs a process and captures its exit code and redirected output.</summary>
    /// <param name="fileName">Executable name or path.</param>
    /// <param name="arguments">Arguments passed without shell interpolation.</param>
    /// <param name="workingDirectory">Directory in which the process starts.</param>
    /// <returns>The completed process result.</returns>
    ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory);
}

