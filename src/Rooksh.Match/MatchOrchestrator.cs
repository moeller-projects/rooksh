using Rooksh.Chess;

namespace Rooksh.Match;

/// <summary>
/// Drives a game between two <see cref="IMoveSource"/>s. Symmetric: it has no notion of
/// "the engine" or "the human", only White and Black. Emits plain events and never renders.
/// When both sources can ponder, the engine not on the clock searches the expected position.
/// </summary>
public sealed class MatchOrchestrator
{
    private readonly IChessGame _game;
    private readonly IMoveSource _white;
    private readonly IMoveSource _black;
    private readonly MatchOptions _options;
    private readonly MatchClock _clock;
    private readonly Dictionary<ChessSide, PendingPonder> _ponders = [];

    private Action<Rooksh.Uci.UciInfo>? _whiteInfoHandler;
    private Action<Rooksh.Uci.UciInfo>? _blackInfoHandler;

    public MatchOrchestrator(
        IChessGame game,
        IMoveSource white,
        IMoveSource black,
        MatchOptions options,
        MatchClock? clock = null)
    {
        _game = game;
        _white = white;
        _black = black;
        _options = options;
        _clock = clock ?? new MatchClock(options.TimeControl);
    }

    public IChessGame Game => _game;

    public MatchClock Clock => _clock;

    public event Action<MatchEvent>? Event;

    /// <summary>Convenience: run to completion with the standard time provider.</summary>
    public static async Task<GameRecord> PlayAsync(
        IChessGame game,
        IMoveSource white,
        IMoveSource black,
        MatchOptions options,
        Action<MatchEvent>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        var orchestrator = new MatchOrchestrator(game, white, black, options);
        if (onEvent is not null)
        {
            orchestrator.Event += onEvent;
        }

        return await orchestrator.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameRecord> RunAsync(CancellationToken cancellationToken = default)
    {
        var record = new GameRecord
        {
            StartFen = _game.IsStandardStart ? null : _game.StartFen,
            WhiteName = _white.DisplayName,
            BlackName = _black.DisplayName,
            EventName = _options.EventName,
            Site = _options.Site,
            Round = _options.Round,
            Date = _options.Date ?? DateTimeOffset.Now,
            TimeControl = _options.TimeControl,
        };

        WireInfo(_white, ChessSide.White);
        WireInfo(_black, ChessSide.Black);

        var ponderEnabled = _options.Ponder
            && _white is IPonderSource { SupportsPonder: true }
            && _black is IPonderSource { SupportsPonder: true };

        Raise(new MatchStartedEvent(record.WhiteName, record.BlackName, _game.StartFen, _options.TimeControl));

        try
        {
            while (!_game.Status.IsOver && !record.Status.IsOver)
            {
                if (_options.MaxPlies is { } maxPlies && record.Moves.Count >= maxPlies)
                {
                    break;
                }

                var side = _game.SideToMove;
                if (_game.LegalMoves.Count == 0)
                {
                    record.Status = _game.Status.IsOver ? _game.Status : GameStatus.Ongoing;
                    break;
                }

                var source = side == ChessSide.White ? _white : _black;
                _clock.StartTurn(side);

                MoveChoice choice;
                string? ponderGuess;
                try
                {
                    (choice, ponderGuess) = await ProduceMoveAsync(side, source, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    record.Status = new GameStatus(GameEndKind.EngineError, side.Opposite());
                    Raise(new EngineFailedEvent(side, source.DisplayName, ex.Message));
                    break;
                }

                if (_clock.HasFlagged(side))
                {
                    record.Status = new GameStatus(GameEndKind.Timeout, side.Opposite());
                    break;
                }

                _clock.CompleteTurn(side);

                if (choice.IsForfeit)
                {
                    record.Status = new GameStatus(GameEndKind.Resignation, side.Opposite());
                    Raise(new EngineFailedEvent(side, source.DisplayName, choice.ForfeitReason ?? "no move"));
                    break;
                }

                var resolved = _game.Resolve(choice.Uci!);
                if (resolved is null)
                {
                    record.Status = new GameStatus(GameEndKind.EngineError, side.Opposite());
                    Raise(new EngineFailedEvent(side, source.DisplayName, $"illegal move '{choice.Uci}' in {_game.Fen}"));
                    break;
                }

                var played = _game.Apply(resolved);
                record.Moves.Add(played);

                Raise(new MovePlayedEvent(
                    played,
                    record.Moves.Count,
                    _clock.Remaining(ChessSide.White),
                    _clock.Remaining(ChessSide.Black)));

                if (_game.Status.IsOver)
                {
                    record.Status = _game.Status;
                    break;
                }

                if (ponderEnabled && ponderGuess is { Length: > 0 })
                {
                    StartPonder(side, ponderGuess);
                }

                Raise(new SideToMoveChangedEvent(_game.SideToMove));
            }

            if (!record.Status.IsOver)
            {
                record.Status = _game.Status.IsOver ? _game.Status : GameStatus.Ongoing;
            }
        }
        finally
        {
            UnwireInfo(_white);
            UnwireInfo(_black);
            await StopPendingPondersAsync().ConfigureAwait(false);
        }

        Raise(new MatchEndedEvent(record.Status, record.Status.Result, record.Status.Termination));
        return record;
    }

    private async Task<(MoveChoice Choice, string? Ponder)> ProduceMoveAsync(
        ChessSide side,
        IMoveSource source,
        CancellationToken cancellationToken)
    {
        if (source is IPonderSource ponderSource && _ponders.Remove(side, out var pending))
        {
            var lastMove = _game.UciHistory.Count > 0 ? _game.UciHistory[^1] : null;
            if (string.Equals(pending.Guess, lastMove, StringComparison.Ordinal))
            {
                var hit = await ponderSource.PonderHitAsync(cancellationToken).ConfigureAwait(false);
                return (EngineMoveSource.ToChoice(hit), hit.Ponder);
            }

            await ponderSource.StopPonderAsync(cancellationToken).ConfigureAwait(false);
        }

        if (source is IPonderSource engine)
        {
            var best = await engine.ThinkAsync(BuildRequest(side), cancellationToken).ConfigureAwait(false);
            return (EngineMoveSource.ToChoice(best), best.Ponder);
        }

        var choice = await source.ChooseMoveAsync(BuildRequest(side), cancellationToken).ConfigureAwait(false);
        return (choice, null);
    }

    private void StartPonder(ChessSide side, string guess)
    {
        var source = side == ChessSide.White ? _white : _black;
        if (source is not IPonderSource { SupportsPonder: true } ponderSource)
        {
            return;
        }

        try
        {
            _ponders[side] = new PendingPonder(guess);
            ponderSource.StartPonder(BuildPonderRequest(side, guess));
        }
        catch
        {
            _ponders.Remove(side);
        }
    }

    private async Task StopPendingPondersAsync()
    {
        foreach (var (side, _) in _ponders.ToArray())
        {
            var source = side == ChessSide.White ? _white : _black;
            if (source is IPonderSource ponderSource)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await ponderSource.StopPonderAsync(timeout.Token).ConfigureAwait(false);
                }
                catch
                {
                    // Best effort; the game is already decided.
                }
            }
        }

        _ponders.Clear();
    }

    private MoveRequest BuildRequest(ChessSide side)
    {
        var incrementMs = _options.TimeControl.Increment > TimeSpan.Zero
            ? (int)_options.TimeControl.Increment.TotalMilliseconds
            : (int?)null;

        return new MoveRequest
        {
            SideToMove = side,
            Fen = _game.Fen,
            StartFen = _game.StartFen,
            IsStandardStart = _game.IsStandardStart,
            UciMoves = _game.UciHistory,
            LegalMoves = _game.LegalMoves,
            WhiteTimeMs = _options.TimeControl.Mode == TimeControlMode.Fixed ? null : _clock.RemainingMilliseconds(ChessSide.White),
            BlackTimeMs = _options.TimeControl.Mode == TimeControlMode.Fixed ? null : _clock.RemainingMilliseconds(ChessSide.Black),
            WhiteIncrementMs = incrementMs,
            BlackIncrementMs = incrementMs,
            MovesToGo = _options.MovesToGo,
            Depth = _options.Depth,
            Nodes = _options.Nodes,
            Ponder = false,
        };
    }

    private MoveRequest BuildPonderRequest(ChessSide side, string guess)
    {
        var moves = new List<string>(_game.UciHistory) { guess };
        return BuildRequest(side) with { UciMoves = moves, Ponder = true };
    }

    private void WireInfo(IMoveSource source, ChessSide side)
    {
        if (source is not IInfoSource infoSource)
        {
            return;
        }

        void Handler(Rooksh.Uci.UciInfo info) =>
            Raise(new EngineInfoEvent(side, source.DisplayName, info));

        infoSource.InfoUpdated += Handler;

        if (side == ChessSide.White)
        {
            _whiteInfoHandler = Handler;
        }
        else
        {
            _blackInfoHandler = Handler;
        }
    }

    private void UnwireInfo(IMoveSource source)
    {
        if (source is not IInfoSource infoSource)
        {
            return;
        }

        if (_whiteInfoHandler is not null && ReferenceEquals(source, _white))
        {
            infoSource.InfoUpdated -= _whiteInfoHandler;
            _whiteInfoHandler = null;
        }

        if (_blackInfoHandler is not null && ReferenceEquals(source, _black))
        {
            infoSource.InfoUpdated -= _blackInfoHandler;
            _blackInfoHandler = null;
        }
    }

    private void Raise(MatchEvent matchEvent) => Event?.Invoke(matchEvent);

    private sealed record PendingPonder(string Guess);
}
