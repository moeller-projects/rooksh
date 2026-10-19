namespace Rooksh.Uci;

/// <summary>
/// The stdio boundary to an engine process. Abstracted so the UCI client can be driven
/// by a scripted in-process fake that still speaks the real line protocol.
/// </summary>
public interface IEngineProcess : IAsyncDisposable
{
    TextWriter StandardInput { get; }

    TextReader StandardOutput { get; }

    bool HasExited { get; }

    int? ExitCode { get; }

    /// <summary>Raised once when the underlying process exits (any reason).</summary>
    event Action<int?>? Exited;

    void Kill();

    Task WaitForExitAsync(CancellationToken cancellationToken = default);
}

/// <summary>Process configuration for one engine slot.</summary>
public sealed record UciEngineOptions
{
    public required string Path { get; init; }

    public IReadOnlyList<string> Args { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    public int? HashMb { get; init; }

    public int? Threads { get; init; }

    public int? MultiPv { get; init; }

    public bool Ponder { get; init; }

    public bool LimitStrength { get; init; }

    public int? UciElo { get; init; }

    /// <summary>Optional extra <c>setoption</c> pairs applied verbatim.</summary>
    public IReadOnlyDictionary<string, string> ExtraOptions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Creates <see cref="IEngineProcess"/> instances.</summary>
public interface IEngineLauncher
{
    IEngineProcess Launch(UciEngineOptions options);
}
