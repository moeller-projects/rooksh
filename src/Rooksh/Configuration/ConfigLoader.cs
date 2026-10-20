using System.Collections;
using System.Globalization;
using LukasMoeller.Configuration.Toml;
using Microsoft.Extensions.Configuration;
using Rooksh.Cli;

namespace Rooksh.Configuration;

/// <summary>Inputs to configuration resolution, ordered by precedence: flags &gt; environment &gt; TOML.</summary>
public sealed class ConfigSources
{
    public required IConfiguration Toml { get; init; }

    public IReadOnlyDictionary<string, string?> Environment { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string?> Flags { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Resolves configuration from TOML, environment and CLI. Each value has exactly one canonical
/// name in every source: <c>[match] base_minutes</c> ↔ <c>ROOKSH_BASE_MINUTES</c> ↔ <c>--base-minutes</c>.
/// </summary>
public static class ConfigLoader
{
    public const string DefaultFileName = "rooksh.toml";

    public static (ParsedCommandLine Cli, RookshConfig Config, string? TomlPath) Load(
        string[] args,
        IDictionary<string, string?>? environment = null,
        string? workingDirectory = null)
    {
        var cli = CommandLineParser.Parse(args);
        var directory = workingDirectory ?? Directory.GetCurrentDirectory();
        var configPath = cli.GetFlag("config") ?? Path.Combine(directory, DefaultFileName);

        var builder = new ConfigurationBuilder();
        if (File.Exists(configPath))
        {
            builder.AddTomlFile(configPath, optional: true);
        }

        var toml = builder.Build();
        var env = ReadEnvironment(environment);

        var sources = new ConfigSources
        {
            Toml = toml,
            Environment = env,
            Flags = cli.Flags,
        };

        return (cli, Resolve(sources), File.Exists(configPath) ? configPath : null);
    }

    public static RookshConfig Resolve(ConfigSources sources)
    {
        return new RookshConfig
        {
            EngineA = ResolveEngine(sources, "engine_a", "ROOKSH_ENGINE_A", "engine-a"),
            EngineB = ResolveEngine(sources, "engine_b", "ROOKSH_ENGINE_B", "engine-b"),
            Match = new MatchConfig
            {
                Mode = GetString(sources, "match:mode", "ROOKSH_MODE", "mode") ?? "engine_vs_engine",
                TimeControl = GetString(sources, "match:time_control", "ROOKSH_TIME_CONTROL", "time-control") ?? "increment",
                BaseMinutes = GetDouble(sources, "match:base_minutes", "ROOKSH_BASE_MINUTES", "base-minutes") ?? 10,
                IncrementSeconds = GetDouble(sources, "match:increment_seconds", "ROOKSH_INCREMENT_SECONDS", "increment-seconds") ?? 2,
                Ponder = GetBool(sources, "match:ponder", "ROOKSH_PONDER", "ponder", fallback: true),
                HumanSide = GetString(sources, "match:human_side", "ROOKSH_HUMAN_SIDE", "human-side") ?? "a",
                Depth = GetInt(sources, "match:depth", "ROOKSH_DEPTH", "depth"),
                Nodes = GetLong(sources, "match:nodes", "ROOKSH_NODES", "nodes"),
            },
            Ui = new UiConfig
            {
                Ascii = GetBool(sources, "ui:ascii", "ROOKSH_ASCII", "ascii", fallback: false),
                ShowCoordinates = GetBool(sources, "ui:show_coordinates", "ROOKSH_SHOW_COORDINATES", "coordinates", fallback: true),
                InfoRefreshMs = GetInt(sources, "ui:info_refresh_ms", "ROOKSH_INFO_REFRESH_MS", "info-refresh-ms") ?? 50,
                MultiPv = GetInt(sources, "ui:multipv", "ROOKSH_MULTIPV", "multipv") ?? 3,
            },
        };
    }

    private static EngineConfig ResolveEngine(ConfigSources sources, string section, string envPrefix, string flagPrefix)
    {
        return new EngineConfig
        {
            Path = GetString(sources, $"{section}:path", $"{envPrefix}_PATH", $"{flagPrefix}-path"),
            Args = GetList(sources, $"{section}:args", $"{envPrefix}_ARGS", $"{flagPrefix}-args"),
            HashMb = GetInt(sources, $"{section}:hash_mb", $"{envPrefix}_HASH_MB", $"{flagPrefix}-hash-mb"),
            Threads = GetInt(sources, $"{section}:threads", $"{envPrefix}_THREADS", $"{flagPrefix}-threads"),
            MultiPv = GetInt(sources, $"{section}:multipv", $"{envPrefix}_MULTIPV", $"{flagPrefix}-multipv"),
            UciElo = GetInt(sources, $"{section}:uci_elo", $"{envPrefix}_UCI_ELO", $"{flagPrefix}-uci-elo"),
            LimitStrength = GetBool(sources, $"{section}:limit_strength", $"{envPrefix}_LIMIT_STRENGTH", $"{flagPrefix}-limit-strength", fallback: false),
        };
    }

    private static string? GetRaw(ConfigSources sources, string tomlKey, string envName, string flag)
    {
        if (sources.Flags.TryGetValue(flag, out var cli) && !string.IsNullOrWhiteSpace(cli))
        {
            return cli;
        }

        if (sources.Environment.TryGetValue(envName, out var env) && !string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var toml = sources.Toml[tomlKey];
        return string.IsNullOrWhiteSpace(toml) ? null : toml;
    }

    private static string? GetString(ConfigSources sources, string tomlKey, string envName, string flag) =>
        GetRaw(sources, tomlKey, envName, flag);

    private static int? GetInt(ConfigSources sources, string tomlKey, string envName, string flag)
    {
        var raw = GetRaw(sources, tomlKey, envName, flag);
        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static long? GetLong(ConfigSources sources, string tomlKey, string envName, string flag)
    {
        var raw = GetRaw(sources, tomlKey, envName, flag);
        return raw is not null && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? GetDouble(ConfigSources sources, string tomlKey, string envName, string flag)
    {
        var raw = GetRaw(sources, tomlKey, envName, flag);
        return raw is not null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool GetBool(ConfigSources sources, string tomlKey, string envName, string flag, bool fallback)
    {
        var raw = GetRaw(sources, tomlKey, envName, flag);
        return raw switch
        {
            null => fallback,
            "1" => true,
            "0" => false,
            _ => bool.TryParse(raw, out var value) ? value : fallback,
        };
    }

    private static IReadOnlyList<string> GetList(ConfigSources sources, string tomlKey, string envName, string flag)
    {
        if (sources.Flags.TryGetValue(flag, out var cli) && !string.IsNullOrWhiteSpace(cli))
        {
            return Split(cli);
        }

        if (sources.Environment.TryGetValue(envName, out var env) && !string.IsNullOrWhiteSpace(env))
        {
            return Split(env);
        }

        var fromToml = sources.Toml.GetSection(tomlKey).GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

        if (fromToml.Count > 0)
        {
            return fromToml;
        }

        var scalar = sources.Toml[tomlKey];
        return string.IsNullOrWhiteSpace(scalar) ? [] : Split(scalar);
    }

    private static IReadOnlyList<string> Split(string value) =>
        value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyDictionary<string, string?> ReadEnvironment(IDictionary<string, string?>? environment)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                result[key] = value;
            }

            return result;
        }

        foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            result[entry.Key.ToString() ?? string.Empty] = entry.Value?.ToString();
        }

        return result;
    }
}
