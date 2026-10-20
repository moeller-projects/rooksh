using System.Globalization;
using System.Threading.Channels;
using Rooksh.Chess;
using Rooksh.Uci;

namespace Rooksh.TestSupport;

/// <summary>Position snapshot a fake engine has been told about.</summary>
public sealed record FakeEnginePosition(string? StartFen, IReadOnlyList<string> Moves)
{
    public static readonly FakeEnginePosition Start = new(null, []);
}

/// <summary>Scripted behavior for a <see cref="FakeEngineProcess"/>.</summary>
public sealed class FakeEngineOptions
{
    public string Name { get; init; } = "FakeEngine";

    public bool AdvertisePonder { get; init; } = true;

    public bool AdvertiseMultiPv { get; init; } = true;

    public bool EmitInfo { get; init; } = true;

    public TimeSpan SearchDelay { get; init; } = TimeSpan.Zero;

    /// <summary>Ponder move attached to every <c>bestmove</c>.</summary>
    public string? PonderMove { get; init; }

    /// <summary>Chooses the move to play given the position and its legal moves. Defaults to the first legal move.</summary>
    public Func<FakeEnginePosition, IReadOnlyList<string>, string?>? MoveSelector { get; init; }

    public IReadOnlyList<string> ExtraOptionLines { get; init; } = [];

    /// <summary>When true the fake exits after replying to the first <c>bestmove</c> (crash simulation).</summary>
    public bool CrashAfterFirstMove { get; init; }
}

/// <summary>
/// In-process UCI engine over channels. The <see cref="UciClient"/> talks to it through a real
/// stdio-shaped endpoint, so the whole protocol path is exercised without spawning a process.
/// </summary>
public sealed class FakeEngineProcess : IEngineProcess
{
    private readonly Channel<string> _commands = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _output = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _loop;
    private readonly FakeEngineOptions _options;
    private readonly object _searchGate = new();
    private readonly List<string> _recorded = [];

    private CancellationTokenSource? _searchCts;
    private FakeEnginePosition _position = FakeEnginePosition.Start;
    private bool _bestMoveEmitted;
    private bool _ponderPending;
    private int _moveCount;

    public FakeEngineProcess(FakeEngineOptions? options = null)
    {
        _options = options ?? new FakeEngineOptions();
        StandardInput = new PipeTextWriter(HandleCommandLine);
        StandardOutput = new PipeTextReader(_output.Reader);
        _loop = Task.Run(RunAsync);
    }

    public TextWriter StandardInput { get; }

    public TextReader StandardOutput { get; }

    public bool HasExited { get; private set; }

    public int? ExitCode { get; private set; }

    public event Action<int?>? Exited;

    public IReadOnlyList<string> RecordedCommands
    {
        get
        {
            lock (_searchGate)
            {
                return _recorded.ToArray();
            }
        }
    }

    public IReadOnlyDictionary<string, string> SetOptions => _setOptions;

    private readonly Dictionary<string, string> _setOptions = new(StringComparer.OrdinalIgnoreCase);

    public void Kill()
    {
        if (HasExited)
        {
            return;
        }

        _shutdown.Cancel();
        _commands.Writer.TryComplete();
        _output.Writer.TryComplete();
        CompleteExit(137);
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // ignore
        }
    }

    public async ValueTask DisposeAsync()
    {
        Kill();
        try
        {
            await _loop.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        _shutdown.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            while (await _commands.Reader.WaitToReadAsync(_shutdown.Token).ConfigureAwait(false))
            {
                while (_commands.Reader.TryRead(out var line))
                {
                    HandleCommandLine(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        finally
        {
            CompleteExit(0);
        }
    }

    private void CompleteExit(int code)
    {
        if (HasExited)
        {
            return;
        }

        HasExited = true;
        ExitCode = code;
        Exited?.Invoke(code);
    }

    private void HandleCommandLine(string line)
    {
        lock (_searchGate)
        {
            _recorded.Add(line);
        }

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (trimmed == "uci")
        {
            Emit($"id name {_options.Name}");
            Emit("id author rooksh-tests");
            if (_options.AdvertisePonder)
            {
                Emit("option name Ponder type check default false");
            }

            if (_options.AdvertiseMultiPv)
            {
                Emit("option name MultiPV type spin default 1 min 1 max 10");
            }

            Emit("option name Hash type spin default 16 min 1 max 4096");
            Emit("option name Threads type spin default 1 min 1 max 128");
            foreach (var extra in _options.ExtraOptionLines)
            {
                Emit(extra);
            }

            Emit("uciok");
            return;
        }

        if (trimmed == "isready")
        {
            Emit("readyok");
            return;
        }

        if (trimmed == "ucinewgame")
        {
            _position = FakeEnginePosition.Start;
            return;
        }

        if (trimmed == "quit")
        {
            _shutdown.Cancel();
            _commands.Writer.TryComplete();
            _output.Writer.TryComplete();
            return;
        }

        if (trimmed.StartsWith("setoption ", StringComparison.Ordinal))
        {
            HandleSetOption(trimmed);
            return;
        }

        if (trimmed.StartsWith("position ", StringComparison.Ordinal))
        {
            HandlePosition(trimmed["position ".Length..]);
            return;
        }

        if (trimmed.StartsWith("go", StringComparison.Ordinal))
        {
            var args = trimmed.Length > 2 ? trimmed[3..] : string.Empty;
            HandleGo(args);
            return;
        }

        if (trimmed == "stop")
        {
            HandleStop();
            return;
        }

        if (trimmed == "ponderhit")
        {
            HandlePonderHit();
        }
    }

    private void HandleSetOption(string line)
    {
        var rest = line["setoption ".Length..];
        if (!rest.StartsWith("name ", StringComparison.Ordinal))
        {
            return;
        }

        rest = rest["name ".Length..];
        var valueMarker = rest.IndexOf(" value ", StringComparison.Ordinal);
        if (valueMarker < 0)
        {
            _setOptions[rest.Trim()] = string.Empty;
            return;
        }

        var name = rest[..valueMarker].Trim();
        var value = rest[(valueMarker + " value ".Length)..].Trim();
        _setOptions[name] = value;
    }

    private void HandlePosition(string rest)
    {
        var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? fen = null;
        var moves = new List<string>();
        var index = 0;

        if (tokens.Length > 0 && tokens[0] == "startpos")
        {
            index = 1;
        }
        else if (tokens.Length > 0 && tokens[0] == "fen")
        {
            index = 1;
            var fields = new List<string>();
            while (index < tokens.Length && tokens[index] != "moves" && fields.Count < 6)
            {
                fields.Add(tokens[index++]);
            }

            fen = string.Join(' ', fields);
        }

        if (index < tokens.Length && tokens[index] == "moves")
        {
            index++;
            for (; index < tokens.Length; index++)
            {
                moves.Add(tokens[index]);
            }
        }

        _position = new FakeEnginePosition(fen, moves);
    }

    private void HandleGo(string goArguments)
    {
        var ponder = goArguments.Split(' ').Any(token => token == "ponder");
        CancellationTokenSource cts;
        lock (_searchGate)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            cts = _searchCts;
            _bestMoveEmitted = false;
            _ponderPending = ponder;
        }

        var move = SelectMove();
        var ponderMove = _options.PonderMove;
        var delay = _options.SearchDelay;

        Emit($"info depth 5 seldepth 7 score cp 25 nodes 12345 nps 100000 time 120 pv {move ?? "(none)"}");

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                }

                if (ponder)
                {
                    // Hold the bestmove until ponderhit/stop, mirroring a real pondering engine.
                    return;
                }

                EmitBestMoveOnce(move, ponderMove);
            }
            catch (OperationCanceledException)
            {
                // stopped
            }
        });
    }

    private void HandleStop()
    {
        CancellationTokenSource? cts;
        lock (_searchGate)
        {
            cts = _searchCts;
        }

        cts?.Cancel();
        EmitBestMoveOnce(SelectMove(), _options.PonderMove);
    }

    private void HandlePonderHit()
    {
        lock (_searchGate)
        {
            if (!_ponderPending)
            {
                return;
            }

            _ponderPending = false;
        }

        EmitBestMoveOnce(SelectMove(), _options.PonderMove);
    }

    private void EmitBestMoveOnce(string? move, string? ponder)
    {
        lock (_searchGate)
        {
            if (_bestMoveEmitted)
            {
                return;
            }

            _bestMoveEmitted = true;
            _ponderPending = false;
            _moveCount++;
        }

        var line = $"bestmove {move ?? "(none)"}";
        if (!string.IsNullOrEmpty(ponder))
        {
            line += $" ponder {ponder}";
        }

        Emit(line);

        if (_options.CrashAfterFirstMove)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(10).ConfigureAwait(false);
                _shutdown.Cancel();
                _commands.Writer.TryComplete();
                _output.Writer.TryComplete();
            });
        }
    }

    private string? SelectMove()
    {
        var legal = LegalMoves();
        if (_options.MoveSelector is { } selector)
        {
            return selector(_position, legal);
        }

        return legal.Count > 0 ? legal[0] : null;
    }

    private IReadOnlyList<string> LegalMoves()
    {
        try
        {
            IChessGame game = GeraChessGameFactory.Instance.Create(_position.StartFen);
            foreach (var move in _position.Moves)
            {
                if (game.Resolve(move) is { } resolved)
                {
                    game.Apply(resolved);
                }
            }

            return game.LegalMoves.Select(m => m.Uci).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private void Emit(string line)
    {
        if (_options.EmitInfo || !line.StartsWith("info", StringComparison.Ordinal))
        {
            _output.Writer.TryWrite(line);
        }
    }
}

/// <summary>Hands out <see cref="FakeEngineProcess"/> instances and records them.</summary>
public sealed class FakeEngineLauncher : IEngineLauncher
{
    private readonly Func<UciEngineOptions, FakeEngineProcess> _factory;

    public FakeEngineLauncher(FakeEngineOptions? options = null)
    {
        _factory = _ => new FakeEngineProcess(options);
    }

    public FakeEngineLauncher(Func<UciEngineOptions, FakeEngineProcess> factory)
    {
        _factory = factory;
    }

    public List<FakeEngineProcess> Processes { get; } = [];

    public IEngineProcess Launch(UciEngineOptions options)
    {
        var process = _factory(options);
        Processes.Add(process);
        return process;
    }
}
