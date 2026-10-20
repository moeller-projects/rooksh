using AutoEndgameRules = global::Chess.AutoEndgameRules;
using ChessBoard = global::Chess.ChessBoard;
using EndgameType = global::Chess.EndgameType;
using Move = global::Chess.Move;
using PieceColor = global::Chess.PieceColor;

namespace Rooksh.Chess;

/// <summary>
/// <see cref="IChessGame"/> over the Gera.Chess rules engine (assembly <c>ChessLibrary</c>).
/// This is the only place in the codebase that references the concrete library.
/// </summary>
public sealed class GeraChessGame : IChessGame
{
    public const string StandardStartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly ChessBoard _board;
    private readonly string _startFen;
    private readonly List<ChessMove> _history = [];

    internal GeraChessGame(ChessBoard board, string? startFen)
    {
        _board = board;
        _startFen = string.IsNullOrWhiteSpace(startFen) ? StandardStartFen : startFen.Trim();
    }

    public ChessSide SideToMove =>
        _board.Turn == PieceColor.White ? ChessSide.White : ChessSide.Black;

    public string Fen => _board.ToFen();

    public string StartFen => _startFen;

    public bool IsStandardStart => string.Equals(_startFen, StandardStartFen, StringComparison.Ordinal);

    public IReadOnlyList<ChessMove> History => _history;

    public IReadOnlyList<string> UciHistory => _history.Select(m => m.Uci).ToArray();

    public GameStatus Status => MapStatus();

    public IReadOnlyList<ChessMove> LegalMoves =>
        _board.Moves(allowAmbiguousCastle: false, generateSan: true).Select(ToChessMove).ToArray();

    public ChessMove Apply(string uciMove)
    {
        var resolved = Resolve(uciMove)
            ?? throw new ChessRuleException($"Illegal move '{uciMove}' in position {Fen}.");
        return ApplyResolved(resolved);
    }

    public ChessMove Apply(ChessMove move)
    {
        var resolved = Resolve(move.Uci)
            ?? throw new ChessRuleException($"Illegal move '{move.Uci}' in position {Fen}.");
        return ApplyResolved(resolved);
    }

    public ChessMove? Resolve(string uciOrSan)
    {
        if (string.IsNullOrWhiteSpace(uciOrSan))
        {
            return null;
        }

        var input = uciOrSan.Trim();
        var uci = ChessMove.NormalizeUci(input);
        var legal = _board.Moves(allowAmbiguousCastle: false, generateSan: true);

        foreach (var candidate in legal)
        {
            if (string.Equals(ToUci(candidate), uci, StringComparison.Ordinal))
            {
                return ToChessMove(candidate);
            }
        }

        // Fall back to SAN (e.g. "Nf3", "O-O", "exd5").
        try
        {
            if (_board.TryParseFromSan(input, out var sanMove, resetSan: true) && sanMove is not null)
            {
                var key = ToUci(sanMove);
                foreach (var candidate in legal)
                {
                    if (string.Equals(ToUci(candidate), key, StringComparison.Ordinal))
                    {
                        return ToChessMove(candidate);
                    }
                }
            }
        }
        catch (Exception)
        {
            // San parse failures are just "not this move"; the caller reports the original input.
        }

        return null;
    }

    public bool UndoLast()
    {
        if (_history.Count == 0)
        {
            return false;
        }

        _board.Cancel();
        _history.RemoveAt(_history.Count - 1);
        return true;
    }

    public string ToPgn() => _board.ToPgn();

    private ChessMove ApplyResolved(ChessMove move)
    {
        var match = _board.Moves(allowAmbiguousCastle: false, generateSan: true)
            .FirstOrDefault(m => string.Equals(ToUci(m), move.Uci, StringComparison.Ordinal))
            ?? throw new ChessRuleException($"Move '{move.Uci}' is not legal in position {Fen}.");

        _board.Move(match);
        var played = ToChessMove(match);
        _history.Add(played);
        return played;
    }

    private ChessMove ToChessMove(Move move) => new(
        Uci: ToUci(move),
        San: move.San ?? string.Empty,
        Side: move.Piece is not null && move.Piece.Color == PieceColor.White ? ChessSide.White : ChessSide.Black,
        IsCapture: move.CapturedPiece is not null,
        IsCheck: move.IsCheck,
        IsMate: move.IsMate);

    private static string ToUci(Move move)
    {
        var promotion = move.Promotion is null
            ? string.Empty
            : char.ToLowerInvariant(move.Promotion.Type.AsChar).ToString();
        return $"{move.OriginalPosition}{move.NewPosition}{promotion}";
    }

    private GameStatus MapStatus()
    {
        if (!_board.IsEndGame || _board.EndGame is null)
        {
            return GameStatus.Ongoing;
        }

        var endgame = _board.EndGame;
        var kind = endgame.EndgameType switch
        {
            EndgameType.Checkmate => GameEndKind.Checkmate,
            EndgameType.Resigned => GameEndKind.Resignation,
            EndgameType.Timeout => GameEndKind.Timeout,
            EndgameType.Stalemate => GameEndKind.Stalemate,
            EndgameType.DrawDeclared => GameEndKind.DrawDeclared,
            EndgameType.InsufficientMaterial => GameEndKind.InsufficientMaterial,
            EndgameType.FiftyMoveRule => GameEndKind.FiftyMoveRule,
            EndgameType.Repetition => GameEndKind.ThreefoldRepetition,
            _ => GameEndKind.Ongoing,
        };

        ChessSide? winner = endgame.WonSide is null
            ? null
            : endgame.WonSide == PieceColor.White ? ChessSide.White : ChessSide.Black;

        return new GameStatus(kind, winner);
    }
}
