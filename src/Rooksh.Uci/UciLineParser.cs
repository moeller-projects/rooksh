using System.Globalization;

namespace Rooksh.Uci;

/// <summary>
/// Stateless, allocation-light parser for the UCI protocol. Kept free of I/O so it is
/// exhaustively unit-testable against canned engine output.
/// </summary>
public static class UciLineParser
{
    private static readonly HashSet<string> InfoKeywords = new(StringComparer.Ordinal)
    {
        "depth", "seldepth", "time", "nodes", "pv", "multipv", "score", "currmove",
        "currmovenumber", "hashfull", "nps", "tbhits", "sbhits", "cpuload", "string",
        "refutation", "currline", "lowerbound", "upperbound",
    };

    /// <summary>Decodes a raw engine line into a <see cref="UciEvent"/>.</summary>
    public static UciEvent Parse(string line)
    {
        line ??= string.Empty;

        if (line.StartsWith("id ", StringComparison.Ordinal) && TryParseId(line, out var id))
        {
            return new UciIdEvent(id) { RawLine = line };
        }

        if (line.StartsWith("option ", StringComparison.Ordinal) && TryParseOption(line, out var option))
        {
            return new UciOptionEvent(option) { RawLine = line };
        }

        if (line == "uciok")
        {
            return new UciOkEvent { RawLine = line };
        }

        if (line == "readyok")
        {
            return new UciReadyOkEvent { RawLine = line };
        }

        if (line.StartsWith("info", StringComparison.Ordinal) && TryParseInfo(line, out var info))
        {
            return new UciInfoEvent(info) { RawLine = line };
        }

        if (line.StartsWith("bestmove", StringComparison.Ordinal) && TryParseBestMove(line, out var bestMove))
        {
            return new UciBestMoveEvent(bestMove) { RawLine = line };
        }

        return new UciRawLineEvent(line) { RawLine = line };
    }

    public static bool TryParseId(string line, out UciId id)
    {
        id = new UciId(null, null);
        if (!line.StartsWith("id ", StringComparison.Ordinal))
        {
            return false;
        }

        var rest = line[3..];
        if (rest.StartsWith("name ", StringComparison.Ordinal))
        {
            id = new UciId(rest[5..].Trim(), null);
            return true;
        }

        if (rest.StartsWith("author ", StringComparison.Ordinal))
        {
            id = new UciId(null, rest[7..].Trim());
            return true;
        }

        return false;
    }

    public static bool TryParseBestMove(string line, out UciBestMove bestMove)
    {
        bestMove = new UciBestMove(null, null);
        if (!line.StartsWith("bestmove", StringComparison.Ordinal))
        {
            return false;
        }

        var tokens = Tokenize(line);
        string? move = tokens.Length > 1 ? tokens[1] : null;
        string? ponder = null;
        for (var i = 2; i < tokens.Length - 1; i++)
        {
            if (tokens[i] == "ponder")
            {
                ponder = tokens[i + 1];
                break;
            }
        }

        bestMove = new UciBestMove(move, ponder);
        return true;
    }

    public static bool TryParseOption(string line, out UciOption option)
    {
        option = new UciOption(string.Empty, UciOptionType.String);
        if (!line.StartsWith("option name ", StringComparison.Ordinal))
        {
            return false;
        }

        var rest = line["option name ".Length..];
        var typeMarker = rest.IndexOf(" type ", StringComparison.Ordinal);
        if (typeMarker < 0)
        {
            return false;
        }

        var name = rest[..typeMarker].Trim();
        var afterType = rest[(typeMarker + " type ".Length)..];
        var typeEnd = afterType.IndexOf(' ');
        var typeText = typeEnd < 0 ? afterType : afterType[..typeEnd];
        var tail = typeEnd < 0 ? string.Empty : afterType[(typeEnd + 1)..];

        var type = typeText.ToLowerInvariant() switch
        {
            "check" => UciOptionType.Check,
            "spin" => UciOptionType.Spin,
            "combo" => UciOptionType.Combo,
            "button" => UciOptionType.Button,
            "string" => UciOptionType.String,
            _ => UciOptionType.String,
        };

        string? defaultValue = null;
        int? min = null;
        int? max = null;
        List<string>? vars = null;

        var tokens = Tokenize(tail);
        for (var i = 0; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "default":
                    if (type == UciOptionType.String)
                    {
                        /* string defaults may contain spaces; take the remainder verbatim */
                        var idx = tail.IndexOf("default ", StringComparison.Ordinal);
                        defaultValue = idx >= 0 ? tail[(idx + "default ".Length)..].Trim() : null;
                        i = tokens.Length;
                    }
                    else if (i + 1 < tokens.Length)
                    {
                        defaultValue = tokens[++i];
                    }

                    break;

                case "min":
                    if (i + 1 < tokens.Length && int.TryParse(tokens[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minValue))
                    {
                        min = minValue;
                    }

                    break;

                case "max":
                    if (i + 1 < tokens.Length && int.TryParse(tokens[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxValue))
                    {
                        max = maxValue;
                    }

                    break;

                case "var":
                    if (i + 1 < tokens.Length)
                    {
                        (vars ??= []).Add(tokens[++i]);
                    }

                    break;
            }
        }

        option = new UciOption(name, type, defaultValue, min, max, vars);
        return true;
    }

    public static bool TryParseInfo(string line, out UciInfo info)
    {
        info = UciInfo.Empty;
        if (!line.StartsWith("info", StringComparison.Ordinal))
        {
            return false;
        }

        var tokens = Tokenize(line);

        int? depth = null, selDepth = null, multiPv = null, nodes = null, nps = null,
            timeMs = null, hashFull = null, tablebaseHits = null, currMoveNumber = null;
        string? currMove = null, text = null;
        var score = UciScore.None;
        List<string>? pv = null;

        for (var i = 1; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "depth":
                    depth = ReadInt(tokens, ref i);
                    break;
                case "seldepth":
                    selDepth = ReadInt(tokens, ref i);
                    break;
                case "multipv":
                    multiPv = ReadInt(tokens, ref i);
                    break;
                case "nodes":
                    nodes = ReadInt(tokens, ref i);
                    break;
                case "nps":
                    nps = ReadInt(tokens, ref i);
                    break;
                case "time":
                    timeMs = ReadInt(tokens, ref i);
                    break;
                case "hashfull":
                    hashFull = ReadInt(tokens, ref i);
                    break;
                case "tbhits":
                    tablebaseHits = ReadInt(tokens, ref i);
                    break;
                case "currmovenumber":
                    currMoveNumber = ReadInt(tokens, ref i);
                    break;
                case "currmove":
                    if (i + 1 < tokens.Length)
                    {
                        currMove = tokens[++i];
                    }

                    break;
                case "score":
                    score = ReadScore(tokens, ref i);
                    break;
                case "pv":
                    pv = ReadUntilKeyword(tokens, ref i);
                    break;
                case "string":
                    text = string.Join(' ', tokens.Skip(i + 1));
                    i = tokens.Length;
                    break;
                case "refutation":
                case "currline":
                    ReadUntilKeyword(tokens, ref i);
                    break;
            }
        }

        info = new UciInfo
        {
            Depth = depth,
            SelDepth = selDepth,
            MultiPv = multiPv,
            Nodes = nodes,
            Nps = nps,
            TimeMs = timeMs,
            HashFull = hashFull,
            TablebaseHits = tablebaseHits,
            CurrMoveNumber = currMoveNumber,
            CurrMove = currMove,
            Score = score,
            Pv = pv ?? [],
            Text = text,
        };

        return true;
    }

    private static int? ReadInt(string[] tokens, ref int index)
    {
        if (index + 1 < tokens.Length
            && int.TryParse(tokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            index++;
            return value;
        }

        return null;
    }

    private static UciScore ReadScore(string[] tokens, ref int index)
    {
        if (index + 2 >= tokens.Length)
        {
            return UciScore.None;
        }

        var kind = tokens[++index];
        if (!int.TryParse(tokens[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return UciScore.None;
        }

        var bound = UciScoreBound.Exact;
        if (index + 1 < tokens.Length)
        {
            if (tokens[index + 1] == "lowerbound")
            {
                bound = UciScoreBound.LowerBound;
                index++;
            }
            else if (tokens[index + 1] == "upperbound")
            {
                bound = UciScoreBound.UpperBound;
                index++;
            }
        }

        return kind switch
        {
            "cp" => new UciScore(value, null, bound),
            "mate" => new UciScore(null, value, bound),
            _ => UciScore.None,
        };
    }

    private static List<string> ReadUntilKeyword(string[] tokens, ref int index)
    {
        var result = new List<string>();
        var j = index + 1;
        while (j < tokens.Length && !InfoKeywords.Contains(tokens[j]))
        {
            result.Add(tokens[j]);
            j++;
        }

        index = j - 1;
        return result;
    }

    private static string[] Tokenize(string line) =>
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
