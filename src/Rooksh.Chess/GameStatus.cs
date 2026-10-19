namespace Rooksh.Chess;

/// <summary>How the game ended (or that it has not).</summary>
public enum GameEndKind
{
    Ongoing,
    Checkmate,
    Stalemate,
    FiftyMoveRule,
    ThreefoldRepetition,
    InsufficientMaterial,
    Resignation,
    Timeout,
    DrawDeclared,
    EngineError,
}

/// <summary>Terminal (or ongoing) state of a game.</summary>
public sealed record GameStatus(GameEndKind Kind, ChessSide? Winner)
{
    public static readonly GameStatus Ongoing = new(GameEndKind.Ongoing, null);

    public bool IsOver => Kind != GameEndKind.Ongoing;

    public bool IsDraw => IsOver && Winner is null;

    /// <summary>PGN result token: <c>1-0</c>, <c>0-1</c>, <c>1/2-1/2</c> or <c>*</c>.</summary>
    public string Result =>
        !IsOver ? "*"
        : Winner is ChessSide.White ? "1-0"
        : Winner is ChessSide.Black ? "0-1"
        : "1/2-1/2";

    /// <summary>Human readable term used for the PGN <c>Termination</c> header.</summary>
    public string Termination => Kind switch
    {
        GameEndKind.Ongoing => "unterminated",
        GameEndKind.Checkmate => "checkmate",
        GameEndKind.Stalemate => "stalemate",
        GameEndKind.FiftyMoveRule => "fifty-move rule",
        GameEndKind.ThreefoldRepetition => "threefold repetition",
        GameEndKind.InsufficientMaterial => "insufficient material",
        GameEndKind.Resignation => "resignation",
        GameEndKind.Timeout => "time forfeit",
        GameEndKind.DrawDeclared => "draw by agreement",
        GameEndKind.EngineError => "engine error",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}
