namespace Rooksh.Chess;

/// <summary>
/// The chess-rules seam. Everything above this interface (orchestrator, renderer,
/// PGN writer) is independent of the concrete rules implementation.
/// </summary>
public interface IChessGame
{
    /// <summary>Side whose turn it is.</summary>
    ChessSide SideToMove { get; }

    /// <summary>Current position in FEN.</summary>
    string Fen { get; }

    /// <summary>Position the game started from.</summary>
    string StartFen { get; }

    /// <summary>True when the game began from the standard initial array.</summary>
    bool IsStandardStart { get; }

    /// <summary>All legal moves for the side to move.</summary>
    IReadOnlyList<ChessMove> LegalMoves { get; }

    /// <summary>Moves played so far, in order.</summary>
    IReadOnlyList<ChessMove> History { get; }

    /// <summary>UCI move list, suitable for a <c>position ... moves</c> command.</summary>
    IReadOnlyList<string> UciHistory { get; }

    /// <summary>Current terminal/ongoing state.</summary>
    GameStatus Status { get; }

    /// <summary>Applies a move given in UCI. Throws <see cref="ChessRuleException"/> when illegal.</summary>
    ChessMove Apply(string uciMove);

    /// <summary>Applies an already-resolved legal move.</summary>
    ChessMove Apply(ChessMove move);

    /// <summary>Resolves a UCI or SAN string to a legal move without playing it, or returns null.</summary>
    ChessMove? Resolve(string uciOrSan);

    /// <summary>Takes back the last move. Returns true when a move was undone.</summary>
    bool UndoLast();

    /// <summary>PGN for the game so far (headers may be incomplete).</summary>
    string ToPgn();
}

/// <summary>Creates games from the standard array or an arbitrary FEN.</summary>
public interface IChessGameFactory
{
    IChessGame Create();

    IChessGame Create(string? fen);
}
