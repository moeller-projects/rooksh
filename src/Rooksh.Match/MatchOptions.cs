using Rooksh.Chess;

namespace Rooksh.Match;

/// <summary>Which participants are human/engine. The orchestrator is symmetric; mode only guides setup.</summary>
public enum MatchMode
{
    EngineVsEngine,
    HumanVsEngine,
    HumanVsHuman,
    Analysis,
}

/// <summary>Everything the orchestrator needs besides the two move sources.</summary>
public sealed record MatchOptions
{
    public MatchMode Mode { get; init; } = MatchMode.EngineVsEngine;

    public TimeControl TimeControl { get; init; } = TimeControl.WithIncrement(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(2));

    /// <summary>Reserved for the M4 ponder milestone; the flag is carried but not yet negotiated here.</summary>
    public bool Ponder { get; init; }

    public int? Depth { get; init; }

    public long? Nodes { get; init; }

    public int? MovesToGo { get; init; }

    /// <summary>Optional hard cap on plies, mainly for deterministic tests and exhibition games.</summary>
    public int? MaxPlies { get; init; }

    public string EventName { get; init; } = "rooksh";

    public string Site { get; init; } = "rooksh";

    public string Round { get; init; } = "-";

    public DateTimeOffset? Date { get; init; }
}
