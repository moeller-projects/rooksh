using System.Text;

namespace Rooksh.Rendering;

/// <summary>
/// Writes a <see cref="ScreenBuffer"/> to a terminal using raw ANSI/VT sequences. Updates are
/// diff-based: only cells that changed since the previous frame are emitted, each preceded by a
/// cursor move, with styling collapsed to minimize escape traffic.
/// </summary>
public sealed class AnsiRenderer : IDisposable
{
    private const string EnterAltScreen = "\u001b[?1049h";
    private const string LeaveAltScreen = "\u001b[?1049l";
    private const string HideCursor = "\u001b[?25l";
    private const string ShowCursor = "\u001b[?25h";
    private const string ClearScreen = "\u001b[2J\u001b[H";
    private const string ResetSgr = "\u001b[0m";
    private const string EnableMouse = "\u001b[?1000h\u001b[?1006h";
    private const string DisableMouse = "\u001b[?1000l\u001b[?1006l";

    private readonly TextWriter _writer;
    private readonly StringBuilder _frame = new();
    private Cell[]? _previous;
    private int _width;
    private int _height;
    private bool _initialized;
    private bool _disposed;

    public AnsiRenderer(TextWriter? writer = null, bool trueColor = true, bool mouse = true)
    {
        _writer = writer ?? Console.Out;
        TrueColor = trueColor;
        MouseEnabled = mouse;
    }

    public bool TrueColor { get; set; }

    public bool MouseEnabled { get; set; }

    public int FramesRendered { get; private set; }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _writer.Write(EnterAltScreen);
        _writer.Write(HideCursor);
        if (MouseEnabled)
        {
            _writer.Write(EnableMouse);
        }

        _writer.Write(ClearScreen);
        _writer.Flush();
    }

    public void Render(ScreenBuffer buffer)
    {
        if (_disposed)
        {
            return;
        }

        if (!_initialized)
        {
            Initialize();
        }

        var sizeChanged = _previous is null || _width != buffer.Width || _height != buffer.Height;
        if (sizeChanged)
        {
            _writer.Write(ClearScreen);
            _previous = new Cell[buffer.Width * buffer.Height];
            Array.Fill(_previous, Cell.Empty);
            _width = buffer.Width;
            _height = buffer.Height;
        }

        _frame.Clear();

        var previous = _previous!;
        Cell? lastStyle = null;
        foreach (var (x, y, cell) in buffer.Diff(previous))
        {
            _frame.Append("\u001b[").Append(y + 1).Append(';').Append(x + 1).Append('H');

            if (lastStyle is not { } style || !StyleEquals(style, cell))
            {
                _frame.Append(ResetSgr);
                AppendSgr(_frame, cell);
                lastStyle = cell;
            }

            _frame.Append(cell.Char);
            previous[(y * _width) + x] = cell;
        }

        if (_frame.Length > 0)
        {
            _frame.Append(ResetSgr);
            _writer.Write(_frame.ToString());
            _writer.Flush();
        }

        _frame.Clear();
        FramesRendered++;
    }

    public void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        _initialized = false;
        if (MouseEnabled)
        {
            _writer.Write(DisableMouse);
        }

        _writer.Write(ResetSgr);
        _writer.Write(ShowCursor);
        _writer.Write(LeaveAltScreen);
        _writer.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Shutdown();
    }

    private static bool StyleEquals(Cell a, Cell b) =>
        a.Foreground.Equals(b.Foreground)
        && a.Background.Equals(b.Background)
        && a.Attributes == b.Attributes;

    private void AppendSgr(StringBuilder sb, Cell cell)
    {
        if (cell.Attributes.HasFlag(TextAttributes.Bold))
        {
            sb.Append("\u001b[1m");
        }

        if (cell.Attributes.HasFlag(TextAttributes.Dim))
        {
            sb.Append("\u001b[2m");
        }

        if (cell.Attributes.HasFlag(TextAttributes.Italic))
        {
            sb.Append("\u001b[3m");
        }

        if (cell.Attributes.HasFlag(TextAttributes.Underline))
        {
            sb.Append("\u001b[4m");
        }

        if (cell.Attributes.HasFlag(TextAttributes.Reverse))
        {
            sb.Append("\u001b[7m");
        }

        AppendColor(sb, cell.Foreground, foreground: true);
        AppendColor(sb, cell.Background, foreground: false);
    }

    private void AppendColor(StringBuilder sb, TermColor color, bool foreground)
    {
        if (color.IsDefault)
        {
            sb.Append(foreground ? "\u001b[39m" : "\u001b[49m");
            return;
        }

        switch (color.Kind)
        {
            case ColorKind.Indexed:
                sb.Append(foreground ? "\u001b[38;5;" : "\u001b[48;5;").Append(color.Index).Append('m');
                break;

            case ColorKind.Rgb when TrueColor:
                sb.Append(foreground ? "\u001b[38;2;" : "\u001b[48;2;")
                    .Append(color.R).Append(';').Append(color.G).Append(';').Append(color.B).Append('m');
                break;

            case ColorKind.Rgb:
                var index = ToAnsi256(color);
                sb.Append(foreground ? "\u001b[38;5;" : "\u001b[48;5;").Append(index).Append('m');
                break;
        }
    }

    internal static byte ToAnsi256(TermColor color)
    {
        if (color.R == color.G && color.G == color.B)
        {
            if (color.R < 8)
            {
                return 16;
            }

            if (color.R > 248)
            {
                return 231;
            }

            return (byte)(232 + (((color.R - 8) * 24) / 247));
        }

        return (byte)(16 + (36 * (color.R / 51)) + (6 * (color.G / 51)) + (color.B / 51));
    }
}
