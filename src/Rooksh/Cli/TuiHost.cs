using System.Text;
using Rooksh.Chess;
using Rooksh.Match;
using Rooksh.Rendering;
using Rooksh.Uci;

namespace Rooksh.Cli;

/// <summary>
/// Interactive terminal host for <c>play</c>. Owns the view state, renders on a periodic tick and
/// on events, and provides the human move source. It works purely from the <see cref="MoveRequest"/>
/// the orchestrator hands it, so it never touches the rules engine.
/// </summary>
public sealed class TuiHost : IDisposable
{
    private readonly AnsiRenderer _renderer;
    private readonly ConsoleTerminal _terminal;
    private readonly bool _ascii;
    private readonly BoardTheme _theme = new();
    private readonly object _gate = new();
    private readonly List<string> _moves = [];
    private readonly CancellationTokenSource _renderCancellation = new();
    private readonly Task _renderLoop;

    private string _fen = MatchViewState.StandardStartFen;
    private bool _flipped;
    private (int File, int Rank)? _selected;
    private (int File, int Rank)? _cursor;
    private (int File, int Rank)? _lastFrom;
    private (int File, int Rank)? _lastTo;
    private IReadOnlyList<(int File, int Rank)> _targets = [];
    private string? _whiteName;
    private string? _blackName;
    private string? _whiteInfo;
    private string? _blackInfo;
    private TimeSpan? _whiteRemaining;
    private TimeSpan? _blackRemaining;
    private bool _firstMoveIsBlack;
    private string? _status;
    private string? _prompt;
    private string _typed = string.Empty;
    private bool _disposed;

    public TuiHost(AnsiRenderer renderer, ConsoleTerminal terminal, bool ascii = false, TimeSpan? refresh = null)
    {
        _renderer = renderer;
        _terminal = terminal;
        _ascii = ascii;
        _renderLoop = Task.Run(() => RenderLoopAsync(refresh ?? TimeSpan.FromMilliseconds(80)));
    }

    public void OnEvent(MatchEvent matchEvent)
    {
        lock (_gate)
        {
            switch (matchEvent)
            {
                case MatchStartedEvent started:
                    _whiteName = started.WhiteName;
                    _blackName = started.BlackName;
                    _fen = started.StartFen;
                    _firstMoveIsBlack = !PieceGlyphs.WhiteToMove(started.StartFen);
                    _moves.Clear();
                    break;

                case MovePlayedEvent move:
                    _moves.Add(move.Move.San);
                    _fen = move.Fen;
                    (_lastFrom, _lastTo) = Squares(move.Move.Uci);
                    _selected = null;
                    _targets = [];
                    _typed = string.Empty;
                    _whiteRemaining = move.WhiteRemaining;
                    _blackRemaining = move.BlackRemaining;
                    break;

                case EngineInfoEvent info:
                    var text = FormatInfo(info.Info);
                    if (info.Side == ChessSide.White)
                    {
                        _whiteInfo = text;
                    }
                    else
                    {
                        _blackInfo = text;
                    }

                    break;

                case EngineFailedEvent failure:
                    _status = $"{failure.Side}: {failure.Message}";
                    break;

                case MatchEndedEvent ended:
                    _status = $"{ended.Result}  {ended.Termination}";
                    break;
            }
        }
    }

    public void Render()
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            var state = BuildState();
            var (width, height) = _terminal.Size;
            var buffer = new ScreenBuffer(width, height);
            MatchScreen.Render(buffer, state, _ascii, _theme);
            _renderer.Render(buffer);
        }
    }

    /// <summary>Blocking input loop that returns the human's chosen move.</summary>
    public MoveChoice PromptForMove(MoveRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _fen = request.Fen;
            _selected = null;
            _targets = [];
            _typed = string.Empty;
            _cursor = request.SideToMove == ChessSide.White ? (4, 1) : (4, 6);
            _prompt = $"{request.SideToMove.Label()} to move — type SAN or select (f flip, q resign)";
        }

        Render();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_terminal.KeyAvailable)
            {
                Thread.Sleep(30);
                continue;
            }

            var keyEvent = _terminal.Read();
            MoveChoice? choice;
            lock (_gate)
            {
                choice = HandleInput(keyEvent, request);
            }

            if (choice is not null)
            {
                lock (_gate)
                {
                    _prompt = null;
                    _typed = string.Empty;
                }

                Render();
                return choice;
            }
        }
    }

    /// <summary>
    /// Leaves the final position on screen until the user explicitly exits. The TUI never closes
    /// on its own; Enter/Escape/q exit, f flips the board, and Ctrl+C cancels via the token.
    /// </summary>
    public void ShowFinished(GameRecord record, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _status = $"{record.Status.Result}  {record.Status.Termination}";
            _whiteRemaining = null;
            _blackRemaining = null;
            _prompt = "Game over — Enter or q exits, f flips";
        }

        Render();

        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            if (!_terminal.KeyAvailable)
            {
                Thread.Sleep(50);
                continue;
            }

            switch (_terminal.Read())
            {
                case KeyEvent { Key: UiKey.Enter or UiKey.Escape }:
                case KeyEvent { Character: 'q' or 'Q' }:
                    return;

                case KeyEvent { Character: 'f' or 'F' }:
                    lock (_gate)
                    {
                        _flipped = !_flipped;
                    }

                    Render();
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _renderCancellation.Cancel();
        try
        {
            _renderLoop.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            // ignore
        }

        _renderCancellation.Dispose();
        _renderer.Dispose();
    }

    private async Task RenderLoopAsync(TimeSpan interval)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(_renderCancellation.Token).ConfigureAwait(false))
            {
                Render();
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private MoveChoice? HandleInput(UiEvent uiEvent, MoveRequest request)
    {
        switch (uiEvent)
        {
            case MouseEvent { Action: MouseAction.Press, Button: MouseButton.Left } mouse:
                var hit = BoardGeometry.HitTest(mouse.X, mouse.Y, MatchScreen.BoardOriginX, MatchScreen.BoardOriginY, _flipped);
                return hit is { } square ? HandleSquare(square, request) : null;

            case MouseEvent { Button: MouseButton.WheelUp }:
                _flipped = !_flipped;
                return null;

            case KeyEvent key:
                return HandleKey(key, request);

            default:
                return null;
        }
    }

    private MoveChoice? HandleKey(KeyEvent key, MoveRequest request)
    {
        switch (key.Key)
        {
            case UiKey.Escape:
                _selected = null;
                _targets = [];
                _typed = string.Empty;
                return null;

            case UiKey.Backspace:
                if (_typed.Length > 0)
                {
                    _typed = _typed[..^1];
                }

                return null;

            case UiKey.Enter:
                if (_typed.Length > 0)
                {
                    return ResolveTyped(request);
                }

                return _cursor is { } enterCursor ? HandleSquare(enterCursor, request) : null;

            case UiKey.Space:
                return _cursor is { } spaceCursor ? HandleSquare(spaceCursor, request) : null;

            case UiKey.Up:
                MoveCursor(0, _flipped ? -1 : 1);
                return null;

            case UiKey.Down:
                MoveCursor(0, _flipped ? 1 : -1);
                return null;

            case UiKey.Left:
                MoveCursor(_flipped ? 1 : -1, 0);
                return null;

            case UiKey.Right:
                MoveCursor(_flipped ? -1 : 1, 0);
                return null;
        }

        if (!key.IsPrintable || key.Control)
        {
            return null;
        }

        var character = key.Character;

        if (_typed.Length == 0)
        {
            if (character is 'f' or 'F')
            {
                _flipped = !_flipped;
                return null;
            }

            if (character is 'q' or 'Q')
            {
                return MoveChoice.Forfeit("resigned");
            }
        }

        if (char.IsLetterOrDigit(character) || character is '=' or '-' or '+' or '#' or 'x')
        {
            _typed += character;
        }

        return null;
    }

    private MoveChoice? ResolveTyped(MoveRequest request)
    {
        var input = _typed.Trim();
        var normalized = ChessMove.NormalizeUci(input);

        var match = request.LegalMoves.FirstOrDefault(m => string.Equals(m.Uci, normalized, StringComparison.OrdinalIgnoreCase))
            ?? request.LegalMoves.FirstOrDefault(m => string.Equals(m.San, input, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            _typed = string.Empty;
            _status = $"Illegal move '{input}'";
            return null;
        }

        return MoveChoice.Of(match.Uci);
    }

    private MoveChoice? HandleSquare((int File, int Rank) square, MoveRequest request)
    {
        if (_selected is { } from)
        {
            if (square == from)
            {
                _selected = null;
                _targets = [];
                return null;
            }

            var candidates = request.LegalMoves
                .Where(m => From(m.Uci) == from && To(m.Uci) == square)
                .ToList();

            if (candidates.Count > 0)
            {
                var chosen = candidates.FirstOrDefault(m => m.Promotion is 'q') ?? candidates[0];
                return MoveChoice.Of(chosen.Uci);
            }
        }

        var targets = request.LegalMoves
            .Where(m => From(m.Uci) == square)
            .Select(m => To(m.Uci))
            .Distinct()
            .ToList();

        if (targets.Count == 0)
        {
            _selected = null;
            _targets = [];
            return null;
        }

        _selected = square;
        _targets = targets;
        _cursor = square;
        return null;
    }

    private void MoveCursor(int fileDelta, int rankDelta)
    {
        var current = _cursor ?? (4, 0);
        _cursor = (Math.Clamp(current.File + fileDelta, 0, 7), Math.Clamp(current.Rank + rankDelta, 0, 7));
    }

    private MatchViewState BuildState()
    {
        return new MatchViewState
        {
            Fen = _fen,
            Flipped = _flipped,
            Selected = _selected,
            Cursor = _cursor,
            LegalTargets = _targets,
            LastFrom = _lastFrom,
            LastTo = _lastTo,
            WhiteName = _whiteName,
            BlackName = _blackName,
            WhiteRemaining = _whiteRemaining,
            BlackRemaining = _blackRemaining,
            WhiteInfo = _whiteInfo,
            BlackInfo = _blackInfo,
            MoveSan = _moves.ToArray(),
            FirstMoveIsBlack = _firstMoveIsBlack,
            Status = _status,
            Prompt = _typed.Length > 0 ? $"{_prompt} [{_typed}]" : _prompt,
        };
    }

    private static string FormatInfo(UciInfo info)
    {
        var sb = new StringBuilder();
        if (info.Depth is { } depth)
        {
            sb.Append("d").Append(depth).Append(' ');
        }

        if (info.Score.HasValue)
        {
            sb.Append(info.Score.ToDisplayString()).Append(' ');
        }

        if (info.Nodes is { } nodes)
        {
            sb.Append(nodes / 1000).Append('k').Append(' ');
        }

        if (info.Nps is { } nps)
        {
            sb.Append(nps / 1000).Append("kn/s ");
        }

        if (info.Pv.Count > 0)
        {
            sb.Append("pv: ").Append(string.Join(' ', info.Pv.Take(4)));
        }

        return sb.ToString().TrimEnd();
    }

    private static ((int File, int Rank) From, (int File, int Rank) To) Squares(string uci) =>
        uci.Length >= 4 ? (From(uci), To(uci)) : ((0, 0), (0, 0));

    private static (int File, int Rank) From(string uci) => ParseSquare(uci, 0);

    private static (int File, int Rank) To(string uci) => ParseSquare(uci, 2);

    private static (int File, int Rank) ParseSquare(string uci, int offset) =>
        (uci[offset] - 'a', uci[offset + 1] - '1');
}

/// <summary>Human move source backed by the <see cref="TuiHost"/> input loop.</summary>
public sealed class TuiHumanMoveSource : IMoveSource
{
    private readonly TuiHost _host;

    public TuiHumanMoveSource(TuiHost host, string displayName = "Human")
    {
        _host = host;
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public Task<MoveChoice> ChooseMoveAsync(MoveRequest request, CancellationToken cancellationToken) =>
        Task.Run(() => _host.PromptForMove(request, cancellationToken), cancellationToken);
}
