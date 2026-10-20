using System.Globalization;

namespace Rooksh.Uci;

/// <summary>Bound qualifier attached to an engine score.</summary>
public enum UciScoreBound
{
    Exact,
    LowerBound,
    UpperBound,
}

/// <summary>
/// An engine evaluation. Exactly one of <see cref="Cp"/> / <see cref="Mate"/> is set
/// when the score is present; <see cref="None"/> means the engine did not report a score.
/// </summary>
public readonly record struct UciScore(int? Cp, int? Mate, UciScoreBound Bound = UciScoreBound.Exact)
{
    public static readonly UciScore None = new(null, null);

    public bool HasValue => Cp.HasValue || Mate.HasValue;

    public bool IsMate => Mate.HasValue;

    /// <summary>Score in centipawns from the side-to-move's point of view (clamped for mate scores).</summary>
    public int AsCentipawns => Cp ?? (Mate is { } m ? (m > 0 ? 10000 - m : -10000 - m) : 0);

    /// <summary>Formatted like <c>+0.38</c> or <c>#3</c>, from the moving side's point of view.</summary>
    public string ToDisplayString()
    {
        if (Mate is { } mate)
        {
            return mate switch
            {
                0 => "#",
                > 0 => $"#{mate}",
                _ => $"#-{-mate}",
            };
        }

        if (Cp is { } cp)
        {
            var pawns = cp / 100.0;
            return (pawns >= 0 ? "+" : string.Empty) + pawns.ToString("0.00", CultureInfo.InvariantCulture);
        }

        return "?";
    }
}
