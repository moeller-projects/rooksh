namespace Rooksh.Uci;

/// <summary>Base type for decoded engine output.</summary>
public abstract record UciEvent
{
    public string RawLine { get; init; } = string.Empty;
}

public sealed record UciIdEvent(UciId Id) : UciEvent;

public sealed record UciOptionEvent(UciOption Option) : UciEvent;

/// <summary>The engine acknowledged <c>uci</c>.</summary>
public sealed record UciOkEvent : UciEvent;

/// <summary>The engine acknowledged <c>isready</c>.</summary>
public sealed record UciReadyOkEvent : UciEvent;

public sealed record UciInfoEvent(UciInfo Info) : UciEvent;

public sealed record UciBestMoveEvent(UciBestMove BestMove) : UciEvent;

/// <summary>Anything we do not model (unknown, <c>copyprotection</c>, stray text, ...).</summary>
public sealed record UciRawLineEvent(string Line) : UciEvent;
