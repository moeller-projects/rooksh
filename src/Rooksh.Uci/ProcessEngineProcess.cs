using System.Diagnostics;

namespace Rooksh.Uci;

/// <summary>Raised when an engine binary cannot be located or started.</summary>
public sealed class EngineStartException : Exception
{
    public EngineStartException(string message) : base(message)
    {
    }

    public EngineStartException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>Starts real engine binaries as child processes with redirected stdio.</summary>
public sealed class ProcessEngineLauncher : IEngineLauncher
{
    private readonly Action<string, string>? _stderrLine;

    public ProcessEngineLauncher(Action<string, string>? stderrLine = null)
    {
        _stderrLine = stderrLine;
    }

    public IEngineProcess Launch(UciEngineOptions options)
    {
        var psi = new ProcessStartInfo(options.Path)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory(),
        };

        foreach (var arg in options.Args)
        {
            psi.ArgumentList.Add(arg);
        }

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                throw new EngineStartException($"Failed to start engine '{options.Path}'.");
            }
        }
        catch (Exception ex) when (ex is not EngineStartException)
        {
            throw new EngineStartException($"Failed to start engine '{options.Path}': {ex.Message}", ex);
        }

        return new ProcessEngineProcess(process, options.Path, _stderrLine);
    }
}

internal sealed class ProcessEngineProcess : IEngineProcess
{
    private readonly Process _process;
    private readonly Task _stderrPump;

    public ProcessEngineProcess(Process process, string displayPath, Action<string, string>? stderrLine)
    {
        _process = process;
        _process.StandardInput.NewLine = "\n";
        _process.Exited += (_, _) => Exited?.Invoke(ExitCode);

        _stderrPump = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
                {
                    stderrLine?.Invoke(displayPath, line);
                }
            }
            catch
            {
                // stderr is diagnostic only; a closed pipe is not worth surfacing.
            }
        });
    }

    public TextWriter StandardInput => _process.StandardInput;

    public TextReader StandardOutput => _process.StandardOutput;

    public bool HasExited => SafeHasExited();

    public int? ExitCode => SafeHasExited() ? _process.ExitCode : null;

    public event Action<int?>? Exited;

    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort; the process may already be gone.
        }
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ignore exit races.
        }
    }

    public async ValueTask DisposeAsync()
    {
        Kill();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        await _stderrPump.ConfigureAwait(false);
        _process.Dispose();
    }

    private bool SafeHasExited()
    {
        try
        {
            return _process.HasExited;
        }
        catch
        {
            return true;
        }
    }
}
