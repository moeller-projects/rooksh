namespace Rooksh.Chess;

/// <summary>
/// A played move. <paramref name="Uci"/> is the canonical engine-facing form
/// (<c>e2e4</c>, <c>e7e8q</c>); <paramref name="San"/> is the human-facing form.
/// </summary>
public sealed record ChessMove(
    string Uci,
    string San,
    ChessSide Side,
    bool IsCapture = false,
    bool IsCheck = false,
    bool IsMate = false)
{
    public string From => Uci.Length >= 4 ? Uci[..2] : Uci;

    public string To => Uci.Length >= 4 ? Uci.Substring(2, 2) : Uci;

    public char? Promotion => Uci.Length >= 5 ? Uci[4] : null;

    public override string ToString() => San;

    /// <summary>Normalizes a user- or engine-supplied UCI string (trims, lower-cases, strips separators).</summary>
    public static string NormalizeUci(string move) =>
        (move ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
}
