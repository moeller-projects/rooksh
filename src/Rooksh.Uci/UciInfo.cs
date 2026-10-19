namespace Rooksh.Uci;

/// <summary>A parsed <c>info</c> line. All fields are optional because engines report different subsets.</summary>
public sealed record UciInfo
{
    public static readonly UciInfo Empty = new();

    public int? Depth { get; init; }

    public int? SelDepth { get; init; }

    public int? MultiPv { get; init; }

    public int? Nodes { get; init; }

    public int? Nps { get; init; }

    public int? TimeMs { get; init; }

    public int? HashFull { get; init; }

    public int? TablebaseHits { get; init; }

    public int? CurrMoveNumber { get; init; }

    public string? CurrMove { get; init; }

    public UciScore Score { get; init; } = UciScore.None;

    public IReadOnlyList<string> Pv { get; init; } = [];

    /// <summary>Free-form <c>info string</c> payload.</summary>
    public string? Text { get; init; }

    public bool IsEmpty =>
        Depth is null && SelDepth is null && MultiPv is null && Nodes is null && Nps is null
        && TimeMs is null && HashFull is null && TablebaseHits is null && CurrMoveNumber is null
        && CurrMove is null && !Score.HasValue && Pv.Count == 0 && Text is null;

    public long? NpsOrNull => Nps;
}
