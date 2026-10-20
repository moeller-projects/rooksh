using System.Text;
using System.Threading.Channels;

namespace Rooksh.Uci;

/// <summary>Result of the initial <c>uci</c>/<c>isready</c> handshake.</summary>
public sealed record UciHandshake(string? Name, string? Author, IReadOnlyList<UciOption> Options)
{
    public bool SupportsPonder =>
        Options.Any(o => string.Equals(o.Name, "Ponder", StringComparison.OrdinalIgnoreCase));

    public bool SupportsMultiPv =>
        Options.Any(o => string.Equals(o.Name, "MultiPV", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Asynchronous, engine-agnostic UCI client. Owns a dedicated reader and writer task so
/// commands such as <c>stop</c> are never queued behind a blocking read.
/// </summary>
public sealed class UciClient : IAsyncDisposable
{
    private readonly IEngineProcess _process;
    private readonly Channel<string> _outbox;
    private readonly object _gate = new();
    private readonly List<UciOption> _options = [];
    private readonly CancellationTokenSource _shutdown = new();

    private Task? _readTask;
    private Task? _writeTask;
    private TaskCompletionSource<bool>? _uciOk;
    private TaskCompletionSource<bool>? _readyOk;
    private TaskCompletionSource<UciBestMove>? _bestMove;
    private UciBestMove? _bufferedBestMove;
    private UciInfo _latestInfo = UciInfo.Empty;
    private int _started;
    private int _disposed;
    private int _processExited;

    public UciClient(IEngineProcess process, string displayName = "engine")
    {
        _process = process;
        DisplayName = displayName;
        _outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public string DisplayName { get; }

    public string? Name { get; private set; }

    public string? Author { get; private set; }

    public IReadOnlyList<UciOption> Options
    {
        get
        {
            lock (_gate)
            {
                return _options.ToArray();
            }
        }
    }

    public UciInfo LatestInfo => Volatile.Read(ref _latestInfo);

    public bool HasExited => _process.HasExited;

    public event Action<UciEvent>? EventReceived;

    public event Action<UciInfo>? InfoReceived;

    public event Action<string>? CommandSent;

    public event Action<int?>? ProcessExited;

    /// <summary>Starts the reader/writer pumps. Idempotent.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _process.Exited += OnProcessExited;
        _writeTask = Task.Run(WriteLoopAsync);
        _readTask = Task.Run(ReadLoopAsync);
    }

    /// <summary>Performs the full <c>uci</c> → <c>uciok</c> → <c>isready</c> → <c>readyok</c> handshake.</summary>
    public async Task<UciHandshake> InitializeAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Start();
        var limit = timeout ?? TimeSpan.FromSeconds(10);

        var uciOk = NewSignal();
        lock (_gate)
        {
            _uciOk = uciOk;
        }

        Send("uci");
        await WaitAsync(uciOk.Task, limit, "uciok", cancellationToken).ConfigureAwait(false);

        await IsReadyAsync(limit, cancellationToken).ConfigureAwait(false);

        return new UciHandshake(Name, Author, Options);
    }

    public async Task IsReadyAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ThrowIfExited();
        var ready = NewSignal();
        lock (_gate)
        {
            _readyOk = ready;
        }

        Send("isready");
        await WaitAsync(ready.Task, timeout ?? TimeSpan.FromSeconds(10), "readyok", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies <c>setoption</c> pairs (a null value emits a valueless option, e.g. buttons) and waits for readiness.</summary>
    public async Task ApplyOptionsAsync(
        IEnumerable<KeyValuePair<string, string?>> options,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var sentAny = false;
        foreach (var (name, value) in options)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            Send(value is null ? $"setoption name {name}" : $"setoption name {name} value {value}");
            sentAny = true;
        }

        if (sentAny)
        {
            await IsReadyAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task NewGameAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Send("ucinewgame");
        await IsReadyAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    public void SetPosition(string? fen, IReadOnlyList<string> moves)
    {
        var sb = new StringBuilder("position ");
        sb.Append(string.IsNullOrWhiteSpace(fen) ? "startpos" : $"fen {fen}");

        if (moves.Count > 0)
        {
            sb.Append(" moves");
            foreach (var move in moves)
            {
                sb.Append(' ').Append(move);
            }
        }

        Send(sb.ToString());
    }

    public void Go(UciGoParameters parameters) => Send(parameters.ToCommand());

    public void Go(string goArguments) => Send($"go {goArguments}");

    public void Stop() => Send("stop");

    public void PonderHit() => Send("ponderhit");

    /// <summary>Waits for the next <c>bestmove</c>. A pending result is buffered if it arrives first.</summary>
    public async Task<UciBestMove> WaitForBestMoveAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<UciBestMove> waiter;
        lock (_gate)
        {
            if (_bufferedBestMove is { } buffered)
            {
                _bufferedBestMove = null;
                return buffered;
            }

            ThrowIfExited();

            waiter = new TaskCompletionSource<UciBestMove>(TaskCreationOptions.RunContinuationsAsynchronously);
            _bestMove = waiter;
        }

        try
        {
            return await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_bestMove, waiter))
                {
                    _bestMove = null;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            _outbox.Writer.TryWrite("quit");
            await Task.Delay(100).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        _shutdown.Cancel();
        _outbox.Writer.TryComplete();
        _process.Kill();

        try
        {
            var pumps = Task.WhenAll(_readTask ?? Task.CompletedTask, _writeTask ?? Task.CompletedTask);
            await pumps.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        await _process.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }

    private void Send(string command)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (_outbox.Writer.TryWrite(command))
        {
            CommandSent?.Invoke(command);
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var command in _outbox.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                try
                {
                    await _process.StandardInput.WriteLineAsync(command).ConfigureAwait(false);
                    await _process.StandardInput.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var line = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                Dispatch(line);
            }
        }
        catch
        {
            // Fall through to failure signalling; engine death is reported below.
        }
        finally
        {
            Interlocked.Exchange(ref _processExited, 1);
            FailPending(new EngineProcessExitedException(DisplayName, _process.ExitCode));
        }
    }

    private void Dispatch(string line)
    {
        var evt = UciLineParser.Parse(line);

        switch (evt)
        {
            case UciIdEvent id:
                lock (_gate)
                {
                    Name ??= id.Id.Name;
                    Author ??= id.Id.Author;
                }

                break;

            case UciOptionEvent option:
                lock (_gate)
                {
                    _options.RemoveAll(o => string.Equals(o.Name, option.Option.Name, StringComparison.OrdinalIgnoreCase));
                    _options.Add(option.Option);
                }

                break;

            case UciOkEvent:
                Signal(ref _uciOk);
                break;

            case UciReadyOkEvent:
                Signal(ref _readyOk);
                break;

            case UciBestMoveEvent bestMove:
                CompleteBestMove(bestMove.BestMove);
                break;

            case UciInfoEvent info:
                Volatile.Write(ref _latestInfo, info.Info);
                InfoReceived?.Invoke(info.Info);
                break;
        }

        EventReceived?.Invoke(evt);
    }

    private void CompleteBestMove(UciBestMove bestMove)
    {
        TaskCompletionSource<UciBestMove>? waiter;
        lock (_gate)
        {
            waiter = _bestMove;
            _bestMove = null;
            if (waiter is null)
            {
                _bufferedBestMove = bestMove;
            }
        }

        waiter?.TrySetResult(bestMove);
    }

    private void Signal(ref TaskCompletionSource<bool>? slot)
    {
        TaskCompletionSource<bool>? waiter;
        lock (_gate)
        {
            waiter = slot;
            slot = null;
        }

        waiter?.TrySetResult(true);
    }

    private void OnProcessExited(int? exitCode)
    {
        Interlocked.Exchange(ref _processExited, 1);
        FailPending(new EngineProcessExitedException(DisplayName, exitCode));
        ProcessExited?.Invoke(exitCode);
    }

    private void ThrowIfExited()
    {
        if (Volatile.Read(ref _processExited) != 0 || _process.HasExited)
        {
            throw new EngineProcessExitedException(DisplayName, _process.ExitCode);
        }
    }

    private void FailPending(Exception exception)
    {
        TaskCompletionSource<bool>? uci;
        TaskCompletionSource<bool>? ready;
        TaskCompletionSource<UciBestMove>? best;
        lock (_gate)
        {
            uci = _uciOk;
            _uciOk = null;
            ready = _readyOk;
            _readyOk = null;
            best = _bestMove;
            _bestMove = null;
        }

        uci?.TrySetException(exception);
        ready?.TrySetException(exception);
        best?.TrySetException(exception);
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitAsync(Task task, TimeSpan timeout, string label, CancellationToken cancellationToken)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, task))
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"Engine did not respond with '{label}' within {timeout.TotalSeconds:0.#}s.");
        }

        await task.ConfigureAwait(false);
    }
}
