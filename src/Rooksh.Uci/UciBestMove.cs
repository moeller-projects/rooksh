namespace Rooksh.Uci;

/// <summary>An engine's chosen move and optional ponder guess.</summary>
public sealed record UciBestMove(string? Move, string? Ponder)
{
    /// <summary>True when the engine reported <c>bestmove (none)</c> or an empty move.</summary>
    public bool IsNone =>
        string.IsNullOrEmpty(Move) || string.Equals(Move, "(none)", StringComparison.OrdinalIgnoreCase);

    public bool HasPonder => !string.IsNullOrEmpty(Ponder) && !string.Equals(Ponder, "(none)", StringComparison.OrdinalIgnoreCase);
}
