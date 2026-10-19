using Rooksh.Chess;

namespace Rooksh.Match;

/// <summary>
/// Tracks remaining time for both sides. Uses <see cref="TimeProvider"/> so tests can drive
/// it deterministically with a fake clock.
/// </summary>
public sealed class MatchClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan[] _remaining = new TimeSpan[2];
    private DateTimeOffset? _turnStarted;
    private ChessSide? _runningSide;

    public MatchClock(TimeControl control, TimeProvider? timeProvider = null)
    {
        Control = control;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _remaining[Index(ChessSide.White)] = control.BaseTime;
        _remaining[Index(ChessSide.Black)] = control.BaseTime;
    }

    public TimeControl Control { get; }

    /// <summary>Remaining time for a side, including the currently running move.</summary>
    public TimeSpan Remaining(ChessSide side)
    {
        var bank = _remaining[Index(side)];
        if (_runningSide == side && _turnStarted is { } started)
        {
            bank -= _timeProvider.GetUtcNow() - started;
        }

        return bank < TimeSpan.Zero ? TimeSpan.Zero : bank;
    }

    /// <summary>Remaining time converted to whole milliseconds, floored at zero.</summary>
    public int RemainingMilliseconds(ChessSide side) => (int)Math.Min(int.MaxValue, Remaining(side).TotalMilliseconds);

    public void StartTurn(ChessSide side)
    {
        _runningSide = side;
        _turnStarted = _timeProvider.GetUtcNow();
    }

    /// <summary>Stops the running clock, applies the move cost and increment, and returns the new remaining time.</summary>
    public TimeSpan CompleteTurn(ChessSide side)
    {
        var elapsed = ElapsedSinceStart();

        _turnStarted = null;
        _runningSide = null;

        if (Control.Mode == TimeControlMode.Fixed)
        {
            _remaining[Index(side)] = Control.BaseTime;
            return Control.BaseTime;
        }

        var left = _remaining[Index(side)] - elapsed + Control.Increment;
        _remaining[Index(side)] = left < TimeSpan.Zero ? TimeSpan.Zero : left;
        return _remaining[Index(side)];
    }

    /// <summary>True when the running side has exhausted its budget.</summary>
    public bool HasFlagged(ChessSide side) =>
        _runningSide == side && Remaining(side) <= TimeSpan.Zero;

    public void SetRemaining(ChessSide side, TimeSpan remaining) =>
        _remaining[Index(side)] = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;

    private TimeSpan ElapsedSinceStart()
    {
        if (_turnStarted is not { } started)
        {
            return TimeSpan.Zero;
        }

        var elapsed = _timeProvider.GetUtcNow() - started;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    private static int Index(ChessSide side) => side == ChessSide.White ? 0 : 1;
}
