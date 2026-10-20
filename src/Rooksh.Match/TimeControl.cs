using Rooksh.Chess;

namespace Rooksh.Match;

/// <summary>How the clock budget behaves across a game.</summary>
public enum TimeControlMode
{
    /// <summary>Each move receives a fresh fixed budget (per-move clock).</summary>
    Fixed,

    /// <summary>Fischer increment: a shared bank plus a per-move bonus.</summary>
    Increment,

    /// <summary>A shared bank for the whole game, no bonus.</summary>
    SuddenDeath,
}

/// <summary>Time control description.</summary>
public sealed record TimeControl(TimeControlMode Mode, TimeSpan BaseTime, TimeSpan Increment)
{
    public static TimeControl WithIncrement(TimeSpan baseTime, TimeSpan increment) =>
        new(TimeControlMode.Increment, baseTime, increment);

    public static TimeControl SuddenDeath(TimeSpan baseTime) =>
        new(TimeControlMode.SuddenDeath, baseTime, TimeSpan.Zero);

    public static TimeControl Fixed(TimeSpan perMove) =>
        new(TimeControlMode.Fixed, perMove, TimeSpan.Zero);

    /// <summary>PGN <c>TimeControl</c> value, e.g. <c>600+5</c> or <c>600</c>.</summary>
    public string ToPgnString() => Mode switch
    {
        TimeControlMode.Fixed => $"*{BaseTime.TotalSeconds:0}",
        TimeControlMode.Increment when Increment > TimeSpan.Zero => $"{BaseTime.TotalSeconds:0}+{Increment.TotalSeconds:0}",
        _ => $"{BaseTime.TotalSeconds:0}",
    };

    public override string ToString() => ToPgnString();
}
