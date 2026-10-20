using System.Globalization;
using System.Text;
using Rooksh.Chess;

namespace Rooksh.Match;

/// <summary>
/// Renders a <see cref="GameRecord"/> to PGN. Pure and deterministic so output can be
/// snapshot-tested; line wrapping is applied at a configurable column.
/// </summary>
public static class PgnSerializer
{
    public const int DefaultWrapColumn = 80;

    public static string Write(GameRecord record, int wrapColumn = DefaultWrapColumn)
    {
        ArgumentNullException.ThrowIfNull(record);

        var sb = new StringBuilder();
        WriteHeader(sb, "Event", record.EventName);
        WriteHeader(sb, "Site", record.Site);
        WriteHeader(sb, "Date", record.Date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture));
        WriteHeader(sb, "Round", record.Round);
        WriteHeader(sb, "White", record.WhiteName);
        WriteHeader(sb, "Black", record.BlackName);
        WriteHeader(sb, "Result", record.Status.Result);

        if (record.TimeControl is { } timeControl)
        {
            WriteHeader(sb, "TimeControl", timeControl.ToPgnString());
        }

        WriteHeader(sb, "Termination", record.Status.Termination);

        if (!string.IsNullOrWhiteSpace(record.StartFen))
        {
            WriteHeader(sb, "SetUp", "1");
            WriteHeader(sb, "FEN", record.StartFen);
        }

        foreach (var (key, value) in record.ExtraHeaders)
        {
            WriteHeader(sb, key, value);
        }

        sb.Append('\n');
        WriteMoveText(sb, record.Moves, record.Status.Result, wrapColumn);
        return sb.ToString();
    }

    /// <summary>Renders just the movetext section (no headers).</summary>
    public static string WriteMoveText(
        IReadOnlyList<ChessMove> moves,
        string result = "*",
        int wrapColumn = DefaultWrapColumn)
    {
        var sb = new StringBuilder();
        WriteMoveText(sb, moves, result, wrapColumn);
        return sb.ToString();
    }

    private static void WriteMoveText(
        StringBuilder sb,
        IReadOnlyList<ChessMove> moves,
        string result,
        int wrapColumn)
    {
        var chunks = new List<string>(moves.Count + 1);
        var number = 1;

        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];

            if (move.Side == ChessSide.White)
            {
                chunks.Add($"{number}. {move.San}");
            }
            else
            {
                var precededByWhite = i > 0 && moves[i - 1].Side == ChessSide.White;
                chunks.Add(precededByWhite ? move.San : $"{number}... {move.San}");
                number++;
            }
        }

        chunks.Add(result);

        var lineLength = 0;
        foreach (var chunk in chunks)
        {
            if (lineLength > 0 && lineLength + 1 + chunk.Length > wrapColumn)
            {
                sb.Append('\n');
                lineLength = 0;
            }

            if (lineLength > 0)
            {
                sb.Append(' ');
                lineLength++;
            }

            sb.Append(chunk);
            lineLength += chunk.Length;
        }

        sb.Append('\n');
    }

    private static void WriteHeader(StringBuilder sb, string key, string value)
    {
        sb.Append('[').Append(key).Append(" \"").Append(EscapeHeader(value)).Append("\"]\n");
    }

    private static string EscapeHeader(string value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
}
