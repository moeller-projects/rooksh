using Rooksh.Chess;

namespace Rooksh.Match;

/// <summary>Accumulated, serializable record of a game.</summary>
public sealed class GameRecord
{
    public string? StartFen { get; init; }

    public string WhiteName { get; init; } = "White";

    public string BlackName { get; init; } = "Black";

    public string EventName { get; init; } = "rooksh";

    public string Site { get; init; } = "rooksh";

    public string Round { get; init; } = "-";

    public DateTimeOffset Date { get; init; } = DateTimeOffset.Now;

    public TimeControl? TimeControl { get; init; }

    public List<ChessMove> Moves { get; } = [];

    public GameStatus Status { get; set; } = GameStatus.Ongoing;

    public Dictionary<string, string> ExtraHeaders { get; } = new(StringComparer.Ordinal);

    public string ToPgn() => PgnSerializer.Write(this);
}
