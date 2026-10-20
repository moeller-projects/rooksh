using System.Text;

namespace Rooksh.Rendering;

/// <summary>
/// Fixed-size 2D grid of <see cref="Cell"/>s. Pure memory: no terminal involved, so layout and
/// diffing can be unit-tested and snapshot-compared.
/// </summary>
public sealed class ScreenBuffer
{
    private readonly Cell[] _cells;

    public ScreenBuffer(int width, int height)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
        _cells = new Cell[width * height];
        Clear();
    }

    public int Width { get; }

    public int Height { get; }

    public Cell this[int x, int y]
    {
        get => InBounds(x, y) ? _cells[(y * Width) + x] : Cell.Empty;
        set
        {
            if (InBounds(x, y))
            {
                _cells[(y * Width) + x] = value;
            }
        }
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Resets every cell to a blank <see cref="Cell.Empty"/>.</summary>
    public void Clear() => Array.Fill(_cells, Cell.Empty);

    public void Clear(Cell fill) => Array.Fill(_cells, fill);

    public void Set(int x, int y, Cell cell) => this[x, y] = cell;

    public void DrawText(int x, int y, string text, Cell style)
    {
        for (var i = 0; i < text.Length; i++)
        {
            this[x + i, y] = style with { Char = text[i] };
        }
    }

    public void DrawText(int x, int y, string text, TermColor foreground, TermColor background, TextAttributes attributes = TextAttributes.None) =>
        DrawText(x, y, text, new Cell(' ', foreground, background, attributes));

    public void FillRect(int x, int y, int width, int height, Cell fill)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                this[column, row] = fill;
            }
        }
    }

    public void HLine(int x, int y, int length, char glyph, Cell style)
    {
        for (var i = 0; i < length; i++)
        {
            this[x + i, y] = style with { Char = glyph };
        }
    }

    public void VLine(int x, int y, int length, char glyph, Cell style)
    {
        for (var i = 0; i < length; i++)
        {
            this[x, y + i] = style with { Char = glyph };
        }
    }

    /// <summary>Draws a box border using single-line glyphs (Unicode) or ASCII fallback.</summary>
    public void DrawBox(int x, int y, int width, int height, Cell style, bool ascii = false)
    {
        if (width < 2 || height < 2)
        {
            return;
        }

        var (topLeft, topRight, bottomLeft, bottomRight, horizontal, vertical) = ascii
            ? ('+', '+', '+', '+', '-', '|')
            : ('┌', '┐', '└', '┘', '─', '│');

        this[x, y] = style with { Char = topLeft };
        this[x + width - 1, y] = style with { Char = topRight };
        this[x, y + height - 1] = style with { Char = bottomLeft };
        this[x + width - 1, y + height - 1] = style with { Char = bottomRight };

        HLine(x + 1, y, width - 2, horizontal, style);
        HLine(x + 1, y + height - 1, width - 2, horizontal, style);
        VLine(x, y + 1, height - 2, vertical, style);
        VLine(x + width - 1, y + 1, height - 2, vertical, style);
    }

    /// <summary>Renders the grid as plain text (one line per row), useful for snapshots.</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                sb.Append(this[x, y].Char);
            }

            if (y < Height - 1)
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>Enumerates the cells that differ from a previous frame.</summary>
    public IEnumerable<(int X, int Y, Cell Cell)> Diff(Cell[]? previous)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = this[x, y];
                var index = (y * Width) + x;
                if (previous is null || index >= previous.Length || !cell.Equals(previous[index]))
                {
                    yield return (x, y, cell);
                }
            }
        }
    }

    public Cell[] ToArray() => (Cell[])_cells.Clone();
}
