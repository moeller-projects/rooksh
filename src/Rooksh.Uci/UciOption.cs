namespace Rooksh.Uci;

/// <summary>UCI option type as advertised by <c>option ... type &lt;t&gt;</c>.</summary>
public enum UciOptionType
{
    Check,
    Spin,
    Combo,
    Button,
    String,
}

/// <summary>A single engine option declaration.</summary>
public sealed record UciOption(
    string Name,
    UciOptionType Type,
    string? Default = null,
    int? Min = null,
    int? Max = null,
    IReadOnlyList<string>? Vars = null)
{
    public IReadOnlyList<string> Values => Vars ?? [];

    public override string ToString() => $"{Name} ({Type})";
}

/// <summary>Engine identity from <c>id name</c> / <c>id author</c>.</summary>
public sealed record UciId(string? Name, string? Author);
