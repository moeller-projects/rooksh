namespace Rooksh.Configuration;

/// <summary>Typed application configuration. One canonical name per value across CLI, env and TOML.</summary>
public sealed class RookshConfig
{
    public EngineConfig EngineA { get; init; } = new();

    public EngineConfig EngineB { get; init; } = new();

    public MatchConfig Match { get; init; } = new();

    public UiConfig Ui { get; init; } = new();
}

public sealed class EngineConfig
{
    public string? Path { get; init; }

    public IReadOnlyList<string> Args { get; init; } = [];

    public int? HashMb { get; init; }

    public int? Threads { get; init; }

    public int? MultiPv { get; init; }

    public int? UciElo { get; init; }

    public bool LimitStrength { get; init; }
}

public sealed class MatchConfig
{
    public string Mode { get; init; } = "engine_vs_engine";

    public string TimeControl { get; init; } = "increment";

    public double BaseMinutes { get; init; } = 10;

    public double IncrementSeconds { get; init; } = 2;

    public bool Ponder { get; init; } = true;

    public string HumanSide { get; init; } = "a";

    public int? Depth { get; init; }

    public long? Nodes { get; init; }
}

public sealed class UiConfig
{
    public bool Ascii { get; init; }

    public bool ShowCoordinates { get; init; } = true;

    public int InfoRefreshMs { get; init; } = 50;

    public int MultiPv { get; init; } = 3;
}
