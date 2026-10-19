using Rooksh.Chess;
using Rooksh.Uci;

namespace Rooksh.Match;

/// <summary>Base type for orchestrator notifications. The UI layer consumes these; the match layer never renders.</summary>
public abstract record MatchEvent;

public sealed record MatchStartedEvent(
    string WhiteName,
    string BlackName,
    string StartFen,
    TimeControl TimeControl) : MatchEvent;

public sealed record MovePlayedEvent(
    ChessMove Move,
    int Ply,
    TimeSpan WhiteRemaining,
    TimeSpan BlackRemaining,
    string Fen) : MatchEvent;

public sealed record SideToMoveChangedEvent(ChessSide SideToMove) : MatchEvent;

public sealed record EngineInfoEvent(ChessSide Side, string EngineName, UciInfo Info) : MatchEvent;

public sealed record EngineFailedEvent(ChessSide Side, string EngineName, string Message) : MatchEvent;

public sealed record MatchEndedEvent(GameStatus Status, string Result, string Termination) : MatchEvent;
