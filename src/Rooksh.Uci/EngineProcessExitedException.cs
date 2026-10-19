namespace Rooksh.Uci;

/// <summary>Raised when an engine process dies while a reply is outstanding.</summary>
public sealed class EngineProcessExitedException : Exception
{
    public EngineProcessExitedException(string displayName, int? exitCode)
        : base($"Engine '{displayName}' exited unexpectedly (exit code {exitCode?.ToString() ?? "unknown"}).")
    {
        DisplayName = displayName;
        ExitCode = exitCode;
    }

    public string DisplayName { get; }

    public int? ExitCode { get; }
}
