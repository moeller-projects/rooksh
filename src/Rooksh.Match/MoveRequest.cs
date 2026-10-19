using Rooksh.Chess;

namespace Rooksh.Match;

/// <summary>Snapshot handed to a move source when it is asked to move.</summary>
public sealed record MoveRequest
{
    public required ChessSide SideToMove { get; init; }

    public required string Fen { get; init; }

    public required string StartFen { get; init; }

    public required bool IsStandardStart { get; init; }

    public required IReadOnlyList<string> UciMoves { get; init; }

    public required IReadOnlyList<ChessMove> LegalMoves { get; init; }

    public int? WhiteTimeMs { get; init; }

    public int? BlackTimeMs { get; init; }

    public int? WhiteIncrementMs { get; init; }

    public int? BlackIncrementMs { get; init; }

    public int? MovesToGo { get; init; }

    public int? Depth { get; init; }

    public long? Nodes { get; init; }

    public bool Ponder { get; init; }
}

/// <summary>A move source's answer: either a UCI move or a forfeit reason.</summary>
public sealed record MoveChoice(string? Uci, string? ForfeitReason)
{
    public static MoveChoice Of(string uci) => new(uci, null);

    public static MoveChoice Forfeit(string reason) => new(null, reason);

    public bool IsForfeit => Uci is null;
}

/// <summary>Anything that can produce a move for the side it is bound to (engine, human, script).</summary>
public interface IMoveSource
{
    string DisplayName { get; }

    Task<MoveChoice> ChooseMoveAsync(MoveRequest request, CancellationToken cancellationToken);
}

/// <summary>Optional capability: a source that streams search information.</summary>
public interface IInfoSource
{
    event Action<Rooksh.Uci.UciInfo>? InfoUpdated;
}
