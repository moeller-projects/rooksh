using System.Text;

namespace Rooksh.Rendering;

/// <summary>Colors used by <see cref="MatchScreen"/>. Kept as data so themes are swappable in tests.</summary>
public sealed record BoardTheme
{
    public TermColor LightSquare { get; init; } = TermColor.Rgb(238, 238, 210);

    public TermColor DarkSquare { get; init; } = TermColor.Rgb(118, 150, 86);

    public TermColor SelectedSquare { get; init; } = TermColor.Rgb(246, 246, 105);

    public TermColor LastMoveSquare { get; init; } = TermColor.Rgb(205, 210, 106);

    public TermColor CursorSquare { get; init; } = TermColor.Rgb(255, 170, 60);

    public TermColor LightSquarePiece { get; init; } = TermColor.Indexed(16);

    public TermColor DarkSquarePiece { get; init; } = TermColor.Indexed(231);

    public TermColor Text { get; init; } = TermColor.Indexed(252);

    public TermColor Muted { get; init; } = TermColor.Indexed(244);

    public TermColor Border { get; init; } = TermColor.Indexed(240);

    public TermColor Accent { get; init; } = TermColor.Indexed(214);

    public TermColor Target { get; init; } = TermColor.Rgb(60, 60, 60);
}

/// <summary>Everything the screen needs to draw a frame. Engine- and rules-agnostic.</summary>
public sealed record MatchViewState
{
    public const string StandardStartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public string Fen { get; init; } = StandardStartFen;

    public bool Flipped { get; init; }

    public (int File, int Rank)? Selected { get; init; }

    public (int File, int Rank)? Cursor { get; init; }

    public IReadOnlyList<(int File, int Rank)> LegalTargets { get; init; } = [];

    public (int File, int Rank)? LastFrom { get; init; }

    public (int File, int Rank)? LastTo { get; init; }

    public string? WhiteName { get; init; }

    public string? BlackName { get; init; }

    public TimeSpan? WhiteRemaining { get; init; }

    public TimeSpan? BlackRemaining { get; init; }

    public string? WhiteInfo { get; init; }

    public string? BlackInfo { get; init; }

    public IReadOnlyList<string> MoveSan { get; init; } = [];

    public bool FirstMoveIsBlack { get; init; }

    public string? Status { get; init; }

    public string? Prompt { get; init; }

    public string? TypedInput { get; init; }
}

/// <summary>Composes the board, coordinates, clocks, engine panels and move list onto a buffer.</summary>
public static class MatchScreen
{
    public const int BoardOriginX = 3;
    public const int BoardOriginY = 2;
    public const int PanelGap = 2;

    public static void Render(ScreenBuffer buffer, MatchViewState state, bool ascii = false, BoardTheme? theme = null)
    {
        theme ??= new BoardTheme();
        buffer.Clear();

        RenderBoard(buffer, state, ascii, theme);
        RenderCoordinates(buffer, state, ascii, theme);
        RenderPanel(buffer, state, ascii, theme);
    }

    public static string RenderToText(MatchViewState state, int width, int height, bool ascii = false)
    {
        var buffer = new ScreenBuffer(width, height);
        Render(buffer, state, ascii);
        return buffer.ToText();
    }

    private static void RenderBoard(ScreenBuffer buffer, MatchViewState state, bool ascii, BoardTheme theme)
    {
        var board = PieceGlyphs.ParseBoard(state.Fen);

        for (var file = 0; file < BoardGeometry.Files; file++)
        {
            for (var rank = 0; rank < BoardGeometry.Ranks; rank++)
            {
                var (x, y) = BoardGeometry.SquareOrigin(file, rank, BoardOriginX, BoardOriginY, state.Flipped);
                var isLight = (file + rank) % 2 == 1;
                var square = (File: file, Rank: rank);
                var isSelected = state.Selected == square;
                var isLastMove = state.LastFrom == square || state.LastTo == square;
                var isCursor = state.Cursor == square;
                var isTarget = state.LegalTargets.Contains(square);

                var background = isSelected ? theme.SelectedSquare
                    : isLastMove ? theme.LastMoveSquare
                    : isCursor ? theme.CursorSquare
                    : isLight ? theme.LightSquare
                    : theme.DarkSquare;

                var piece = board[file, rank];
                var foreground = isLight ? theme.LightSquarePiece : theme.DarkSquarePiece;
                var glyph = PieceGlyphs.Glyph(piece, ascii);

                if (glyph == ' ' && isTarget)
                {
                    glyph = ascii ? '*' : '·';
                    foreground = theme.Target;
                }

                buffer.Set(x, y, new Cell(glyph, foreground, background));
                buffer.Set(x + 1, y, new Cell(' ', foreground, background));
            }
        }
    }

    private static void RenderCoordinates(ScreenBuffer buffer, MatchViewState state, bool ascii, BoardTheme theme)
    {
        var style = new Cell(' ', theme.Muted, TermColor.Default);

        for (var rank = 0; rank < BoardGeometry.Ranks; rank++)
        {
            var (_, y) = BoardGeometry.SquareOrigin(0, rank, BoardOriginX, BoardOriginY, state.Flipped);
            buffer.Set(BoardOriginX - 1, y, style with { Char = (char)('1' + rank) });
        }

        for (var file = 0; file < BoardGeometry.Files; file++)
        {
            var (x, _) = BoardGeometry.SquareOrigin(file, 0, BoardOriginX, BoardOriginY, state.Flipped);
            buffer.Set(x, BoardOriginY + BoardGeometry.DisplayHeight, style with { Char = (char)('a' + file) });
        }
    }

    private static void RenderPanel(ScreenBuffer buffer, MatchViewState state, bool ascii, BoardTheme theme)
    {
        var panelX = BoardOriginX + BoardGeometry.DisplayWidth + PanelGap;
        var panelWidth = Math.Max(0, buffer.Width - panelX - 1);
        if (panelWidth < 8)
        {
            return;
        }

        var y = BoardOriginY;
        y = WriteLine(buffer, panelX, y, panelWidth, "rooksh — terminal chess", theme.Accent, theme, bold: true);
        y = WriteLine(buffer, panelX, y, panelWidth, new string(ascii ? '-' : '─', Math.Min(panelWidth, 30)), theme.Border, theme);

        y = WriteEngineBlock(buffer, panelX, y, panelWidth, "A / WHITE", state.WhiteName, state.WhiteRemaining, state.WhiteInfo, theme);
        y = WriteEngineBlock(buffer, panelX, y, panelWidth, "B / BLACK", state.BlackName, state.BlackRemaining, state.BlackInfo, theme);

        y = WriteLine(buffer, panelX, y, panelWidth, new string(ascii ? '-' : '─', Math.Min(panelWidth, 30)), theme.Border, theme);
        y = WriteLine(buffer, panelX, y, panelWidth, "MOVES", theme.Accent, theme, bold: true);

        foreach (var line in BuildMoveLines(state))
        {
            if (y >= buffer.Height - 3)
            {
                break;
            }

            y = WriteLine(buffer, panelX, y, panelWidth, line, theme.Text, theme);
        }

        if (state.Status is { Length: > 0 })
        {
            WriteLine(buffer, panelX, Math.Max(y + 1, buffer.Height - 3), panelWidth, state.Status, theme.Accent, theme, bold: true);
        }

        if (state.Prompt is { Length: > 0 })
        {
            var prompt = state.TypedInput is { Length: > 0 } typed ? $"{state.Prompt} {typed}" : state.Prompt;
            WriteLine(buffer, panelX, buffer.Height - 2, panelWidth, prompt, theme.Text, theme);
        }
    }

    private static int WriteEngineBlock(
        ScreenBuffer buffer,
        int x,
        int y,
        int width,
        string title,
        string? name,
        TimeSpan? remaining,
        string? info,
        BoardTheme theme)
    {
        y = WriteLine(buffer, x, y, width, title, theme.Accent, theme, bold: true);
        var header = $"{name ?? "—"}  {FormatClock(remaining)}";
        y = WriteLine(buffer, x, y, width, header, theme.Text, theme);
        if (info is { Length: > 0 })
        {
            y = WriteLine(buffer, x, y, width, info, theme.Muted, theme);
        }

        return y;
    }

    private static IEnumerable<string> BuildMoveLines(MatchViewState state)
    {
        var moves = state.MoveSan;
        if (moves.Count == 0)
        {
            yield break;
        }

        var index = 0;
        var number = 1;
        if (state.FirstMoveIsBlack)
        {
            yield return $"{number,3}. ... {moves[0]}";
            index = 1;
            number++;
        }

        for (; index < moves.Count; index += 2)
        {
            var white = moves[index];
            var black = index + 1 < moves.Count ? moves[index + 1] : string.Empty;
            yield return $"{number,3}. {white,-8} {black}".TrimEnd();
            number++;
        }
    }

    private static int WriteLine(
        ScreenBuffer buffer,
        int x,
        int y,
        int width,
        string text,
        TermColor foreground,
        BoardTheme theme,
        bool bold = false)
    {
        var attributes = bold ? TextAttributes.Bold : TextAttributes.None;
        buffer.DrawText(x, y, Truncate(text, width), foreground, TermColor.Default, attributes);
        return y + 1;
    }

    private static string Truncate(string text, int width) =>
        text.Length <= width ? text : text[..Math.Max(0, width)];

    private static string FormatClock(TimeSpan? remaining)
    {
        if (remaining is not { } time)
        {
            return "--:--";
        }

        var clamped = time < TimeSpan.Zero ? TimeSpan.Zero : time;
        return clamped.TotalHours >= 1
            ? $"{(int)clamped.TotalHours}:{clamped.Minutes:00}:{clamped.Seconds:00}"
            : $"{clamped.Minutes}:{clamped.Seconds:00}";
    }
}
