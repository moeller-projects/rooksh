using System.Globalization;
using System.Text;

namespace Rooksh.Uci;

/// <summary>
/// Typed representation of a UCI <c>go</c> command. Rendering is centralized so the
/// exact wire format can be asserted in tests.
/// </summary>
public sealed record UciGoParameters
{
    public int? WhiteTimeMs { get; init; }

    public int? BlackTimeMs { get; init; }

    public int? WhiteIncrementMs { get; init; }

    public int? BlackIncrementMs { get; init; }

    public int? MovesToGo { get; init; }

    public int? Depth { get; init; }

    public long? Nodes { get; init; }

    public int? Mate { get; init; }

    public int? MoveTimeMs { get; init; }

    public bool Infinite { get; init; }

    public bool Ponder { get; init; }

    public IReadOnlyList<string> SearchMoves { get; init; } = [];

    /// <summary>Renders the argument portion of <c>go ...</c>. Never returns an empty string.</summary>
    public string ToArguments()
    {
        var sb = new StringBuilder();

        void Append(string token)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(token);
        }

        if (SearchMoves.Count > 0)
        {
            Append("searchmoves");
            foreach (var move in SearchMoves)
            {
                Append(move);
            }
        }

        if (Ponder)
        {
            Append("ponder");
        }

        if (WhiteTimeMs is { } wtime)
        {
            Append($"wtime {wtime.ToString(CultureInfo.InvariantCulture)}");
        }

        if (BlackTimeMs is { } btime)
        {
            Append($"btime {btime.ToString(CultureInfo.InvariantCulture)}");
        }

        if (WhiteIncrementMs is { } winc)
        {
            Append($"winc {winc.ToString(CultureInfo.InvariantCulture)}");
        }

        if (BlackIncrementMs is { } binc)
        {
            Append($"binc {binc.ToString(CultureInfo.InvariantCulture)}");
        }

        if (MovesToGo is { } movestogo)
        {
            Append($"movestogo {movestogo.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Depth is { } depth)
        {
            Append($"depth {depth.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Nodes is { } nodes)
        {
            Append($"nodes {nodes.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Mate is { } mate)
        {
            Append($"mate {mate.ToString(CultureInfo.InvariantCulture)}");
        }

        if (MoveTimeMs is { } movetime)
        {
            Append($"movetime {movetime.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Infinite)
        {
            Append("infinite");
        }

        return sb.Length == 0 ? "infinite" : sb.ToString();
    }

    public string ToCommand() => $"go {ToArguments()}";
}
