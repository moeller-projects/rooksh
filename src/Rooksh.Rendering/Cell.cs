namespace Rooksh.Rendering;

public enum ColorKind
{
    Default,
    Indexed,
    Rgb,
}

/// <summary>Terminal color: default, 256-palette index, or truecolor.</summary>
public readonly record struct TermColor(ColorKind Kind, byte Index, byte R, byte G, byte B)
{
    public static readonly TermColor Default = new(ColorKind.Default, 0, 0, 0, 0);

    public static TermColor Indexed(byte index) => new(ColorKind.Indexed, index, 0, 0, 0);

    public static TermColor Rgb(byte r, byte g, byte b) => new(ColorKind.Rgb, 0, r, g, b);

    public bool IsDefault => Kind == ColorKind.Default;
}

[Flags]
public enum TextAttributes
{
    None = 0,
    Bold = 1,
    Dim = 2,
    Italic = 4,
    Underline = 8,
    Reverse = 16,
}

/// <summary>One character cell: glyph plus foreground, background and attributes.</summary>
public readonly record struct Cell(char Char, TermColor Foreground, TermColor Background, TextAttributes Attributes = TextAttributes.None)
{
    public static readonly Cell Empty = new(' ', TermColor.Default, TermColor.Default, TextAttributes.None);

    public Cell(char c) : this(c, TermColor.Default, TermColor.Default, TextAttributes.None)
    {
    }

    public bool IsBlank => Char == ' ' && Foreground.IsDefault && Background.IsDefault && Attributes == TextAttributes.None;
}
