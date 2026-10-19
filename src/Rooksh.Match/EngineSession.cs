using Rooksh.Uci;

namespace Rooksh.Match;

/// <summary>
/// A live engine process: handshake, option negotiation, and turn-by-turn search.
/// Owns the <see cref="UciClient"/>.
/// </summary>
public sealed class EngineSession : IAsyncDisposable
{
    private readonly UciEngineOptions _options;
    private readonly IEngineLauncher _launcher;
    private readonly Action<string, string>? _trace;
    private UciClient? _client;
    private UciHandshake? _handshake;

    public EngineSession(
        string slot,
        UciEngineOptions options,
        IEngineLauncher launcher,
        Action<string, string>? trace = null)
    {
        Slot = slot;
        _options = options;
        _launcher = launcher;
        _trace = trace;
        DisplayName = System.IO.Path.GetFileNameWithoutExtension(options.Path) is { Length: > 0 } file
            ? file
            : options.Path;
    }

    public string Slot { get; }

    public string DisplayName { get; private set; }

    public UciHandshake? Handshake => _handshake;

    public UciInfo LatestInfo => _client?.LatestInfo ?? UciInfo.Empty;

    public bool SupportsPonder => _handshake?.SupportsPonder ?? false;

    public event Action<UciInfo>? InfoUpdated;

    public event Action<string, string>? Trace;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var process = _launcher.Launch(_options);
        _client = new UciClient(process, DisplayName);
        _client.InfoReceived += info => InfoUpdated?.Invoke(info);
        _client.EventReceived += evt => Trace?.Invoke(Slot, "< " + evt.RawLine);
        _client.CommandSent += command => Trace?.Invoke(Slot, "> " + command);

        _handshake = await _client.InitializeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_handshake.Name))
        {
            DisplayName = _handshake.Name!;
        }

        await ApplyOptionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task NewGameAsync(CancellationToken cancellationToken = default) =>
        Client.NewGameAsync(cancellationToken: cancellationToken);

    /// <summary>Sets a single engine option and waits for readiness.</summary>
    public Task SetOptionAsync(string name, string? value, CancellationToken cancellationToken = default) =>
        Client.ApplyOptionsAsync([new KeyValuePair<string, string?>(name, value)], cancellationToken: cancellationToken);

    /// <summary>Sends <c>position</c> + <c>go</c> and awaits the engine's <c>bestmove</c>.</summary>
    public async Task<UciBestMove> ThinkAsync(MoveRequest request, bool ponder, CancellationToken cancellationToken = default)
    {
        Client.SetPosition(request.IsStandardStart ? null : request.StartFen, request.UciMoves);
        Client.Go(BuildGoParameters(request, ponder));
        return await Client.WaitForBestMoveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts a search without awaiting it (used by analysis mode).</summary>
    public void StartSearch(string? startFen, IReadOnlyList<string> moves, UciGoParameters parameters)
    {
        Client.SetPosition(startFen, moves);
        Client.Go(parameters);
    }

    /// <summary>Awaits the next <c>bestmove</c> from a search started via <see cref="StartSearch"/>.</summary>
    public Task<UciBestMove> AwaitBestMoveAsync(CancellationToken cancellationToken = default) =>
        Client.WaitForBestMoveAsync(cancellationToken);

    /// <summary>Sends a ponder search without awaiting the result (M4 ponder milestone).</summary>
    public void StartPonder(MoveRequest request)
    {
        Client.SetPosition(request.IsStandardStart ? null : request.StartFen, request.UciMoves);
        Client.Go(BuildGoParameters(request, ponder: true));
    }

    /// <summary>Confirms a ponder guess; the engine keeps its accumulated search.</summary>
    public Task<UciBestMove> PonderHitAsync(CancellationToken cancellationToken = default)
    {
        Client.PonderHit();
        return Client.WaitForBestMoveAsync(cancellationToken);
    }

    /// <summary>Stops a running search and drains the resulting bestmove.</summary>
    public async Task<UciBestMove> StopAsync(CancellationToken cancellationToken = default)
    {
        Client.Stop();
        return await Client.WaitForBestMoveAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Stop() => Client.Stop();

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }

    private UciClient Client => _client ?? throw new InvalidOperationException("Engine session has not been initialized.");

    private async Task ApplyOptionsAsync(CancellationToken cancellationToken)
    {
        var options = new List<KeyValuePair<string, string?>>();

        if (_options.HashMb is { } hash)
        {
            options.Add(new("Hash", hash.ToString()));
        }

        if (_options.Threads is { } threads)
        {
            options.Add(new("Threads", threads.ToString()));
        }

        if (_options.MultiPv is { } multiPv)
        {
            options.Add(new("MultiPV", multiPv.ToString()));
        }

        if (_handshake?.SupportsPonder == true)
        {
            options.Add(new("Ponder", _options.Ponder ? "true" : "false"));
        }

        if (_options.LimitStrength)
        {
            options.Add(new("UCI_LimitStrength", "true"));
            if (_options.UciElo is { } elo)
            {
                options.Add(new("UCI_Elo", elo.ToString()));
            }
        }

        foreach (var (key, value) in _options.ExtraOptions)
        {
            options.Add(new(key, value));
        }

        await Client.ApplyOptionsAsync(options, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static UciGoParameters BuildGoParameters(MoveRequest request, bool ponder)
    {
        var parameters = new UciGoParameters
        {
            WhiteTimeMs = request.WhiteTimeMs,
            BlackTimeMs = request.BlackTimeMs,
            WhiteIncrementMs = request.WhiteIncrementMs,
            BlackIncrementMs = request.BlackIncrementMs,
            MovesToGo = request.MovesToGo,
            Depth = request.Depth,
            Nodes = request.Nodes,
            Ponder = ponder,
        };

        var hasLimit = parameters.Depth is not null
            || parameters.Nodes is not null
            || parameters.WhiteTimeMs is not null
            || parameters.BlackTimeMs is not null
            || parameters.MoveTimeMs is not null;

        return hasLimit ? parameters : parameters with { Infinite = true };
    }
}
