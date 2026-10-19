namespace Rooksh.Cli;

/// <summary>Result of tokenizing <c>args</c>: a command, positionals, and typed-enough flags.</summary>
public sealed record ParsedCommandLine
{
    public static readonly ParsedCommandLine Empty = new();

    public string Command { get; init; } = string.Empty;

    public IReadOnlyList<string> Positionals { get; init; } = [];

    public IReadOnlyDictionary<string, string?> Flags { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public bool HasFlag(string name) => Flags.ContainsKey(name);

    public string? GetFlag(string name) =>
        Flags.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// Small, dependency-free argument parser. Flags are <c>--name</c>, <c>--name value</c> or
/// <c>--name=value</c>; a fixed set of boolean flags never consumes the next token.
/// </summary>
public static class CommandLineParser
{
    private static readonly HashSet<string> BooleanFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "ascii", "no-ascii",
        "coordinates", "no-coordinates",
        "ponder", "no-ponder",
        "infinite",
        "verbose",
        "help", "h",
        "version", "v",
        "debug",
    };

    public static ParsedCommandLine Parse(string[] args)
    {
        var command = string.Empty;
        var positionals = new List<string>();
        var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (token.StartsWith("--", StringComparison.Ordinal) || (token.Length > 1 && token[0] == '-' && !char.IsDigit(token[1])))
            {
                var name = token.TrimStart('-');
                string? value = null;

                var equals = name.IndexOf('=');
                if (equals >= 0)
                {
                    value = name[(equals + 1)..];
                    name = name[..equals];
                }
                else if (!BooleanFlags.Contains(name) && i + 1 < args.Length && !IsFlag(args[i + 1]))
                {
                    value = args[++i];
                }

                if (BooleanFlags.Contains(name))
                {
                    flags[name] = value ?? "true";
                }
                else
                {
                    flags[name] = value;
                }

                continue;
            }

            if (command.Length == 0)
            {
                command = token;
            }
            else
            {
                positionals.Add(token);
            }
        }

        if (flags.ContainsKey("no-ascii"))
        {
            flags["ascii"] = "false";
        }

        if (flags.ContainsKey("no-coordinates"))
        {
            flags["coordinates"] = "false";
        }

        if (flags.ContainsKey("no-ponder"))
        {
            flags["ponder"] = "false";
        }

        return new ParsedCommandLine
        {
            Command = command,
            Positionals = positionals,
            Flags = flags,
        };
    }

    private static bool IsFlag(string token) =>
        token.StartsWith("--", StringComparison.Ordinal)
        || (token.Length > 1 && token[0] == '-' && !char.IsDigit(token[1]));
}
