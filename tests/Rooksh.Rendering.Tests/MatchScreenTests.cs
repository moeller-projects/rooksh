using Rooksh.Rendering;

namespace Rooksh.Rendering.Tests;

public class BoardGeometryTests
{
    [Fact]
    public void Parses_start_position_into_board()
    {
        var board = PieceGlyphs.ParseBoard(MatchViewState.StandardStartFen);

        board[0, 0].ShouldBe('R'); // a1
        board[4, 0].ShouldBe('K'); // e1
        board[0, 7].ShouldBe('r'); // a8
        board[4, 7].ShouldBe('k'); // e8
        board[4, 1].ShouldBe('P'); // e2
        board[4, 6].ShouldBe('p'); // e7
        board[4, 4].ShouldBe(' '); // e4
    }

    [Fact]
    public void Glyph_maps_white_and_black_pieces()
    {
        PieceGlyphs.Glyph('K').ShouldBe('♔');
        PieceGlyphs.Glyph('k').ShouldBe('♚');
        PieceGlyphs.Glyph('Q').ShouldBe('♕');
        PieceGlyphs.Glyph('q').ShouldBe('♛');
        PieceGlyphs.Glyph('N').ShouldBe('♘');
        PieceGlyphs.Glyph('K', ascii: true).ShouldBe('K');
        PieceGlyphs.Glyph('k', ascii: true).ShouldBe('k');
        PieceGlyphs.Glyph(' ').ShouldBe(' ');
    }

    [Fact]
    public void Hit_test_matches_square_origin()
    {
        const int originX = 3;
        const int originY = 2;

        for (var file = 0; file < 8; file++)
        {
            for (var rank = 0; rank < 8; rank++)
            {
                var (x, y) = BoardGeometry.SquareOrigin(file, rank, originX, originY, flipped: false);
                BoardGeometry.HitTest(x, y, originX, originY, flipped: false).ShouldBe((file, rank));
                BoardGeometry.HitTest(x + 1, y, originX, originY, flipped: false).ShouldBe((file, rank));
            }
        }
    }

    [Fact]
    public void Flipped_hit_test_matches_flipped_origin()
    {
        const int originX = 3;
        const int originY = 2;

        var (x, y) = BoardGeometry.SquareOrigin(0, 0, originX, originY, flipped: true);
        BoardGeometry.HitTest(x, y, originX, originY, flipped: true).ShouldBe((0, 0));
        BoardGeometry.HitTest(x, y, originX, originY, flipped: false).ShouldBe((7, 7));
    }

    [Fact]
    public void Hit_test_outside_board_returns_null()
    {
        BoardGeometry.HitTest(0, 0, 3, 2, flipped: false).ShouldBeNull();
        BoardGeometry.HitTest(19, 2, 3, 2, flipped: false).ShouldBeNull();
    }
}

public class MatchScreenTests
{
    [Fact]
    public void Renders_standard_start_position()
    {
        var text = MatchScreen.RenderToText(new MatchViewState(), 48, 14);

        text.ShouldContain("♜");
        text.ShouldContain("♙");
        text.ShouldContain("rooksh");
        text.ShouldContain("A / WHITE");
        text.ShouldContain("B / BLACK");
    }

    [Fact]
    public void Ascii_mode_uses_letters()
    {
        var text = MatchScreen.RenderToText(new MatchViewState(), 48, 14, ascii: true);

        text.ShouldContain("R");
        text.ShouldContain("P");
        text.ShouldNotContain("♜");
    }

    [Fact]
    public void Renders_move_list_and_status()
    {
        var state = new MatchViewState
        {
            MoveSan = ["e4", "e5", "Nf3", "Nc6"],
            Status = "1-0 checkmate",
            WhiteName = "Zwischenzug",
            BlackName = "Stockfish",
            WhiteRemaining = TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(32),
            BlackRemaining = TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(47),
        };

        var text = MatchScreen.RenderToText(state, 48, 16);

        text.ShouldContain("1. e4");
        text.ShouldContain("2. Nf3");
        text.ShouldContain("checkmate");
        text.ShouldContain("4:32");
        text.ShouldContain("Zwischenzug");
    }

    [Fact]
    public void Highlights_last_move_and_targets()
    {
        var theme = new BoardTheme();
        var buffer = new ScreenBuffer(48, 16);
        var state = new MatchViewState
        {
            Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            LastFrom = (4, 1),
            LastTo = (4, 3),
            Selected = (4, 1),
            LegalTargets = [(4, 2), (4, 3)],
        };

        MatchScreen.Render(buffer, state, ascii: true, theme);

        var (fromX, fromY) = BoardGeometry.SquareOrigin(4, 1, MatchScreen.BoardOriginX, MatchScreen.BoardOriginY, false);
        buffer[fromX, fromY].Background.ShouldBe(theme.SelectedSquare);

        var (toX, toY) = BoardGeometry.SquareOrigin(4, 3, MatchScreen.BoardOriginX, MatchScreen.BoardOriginY, false);
        buffer[toX, toY].Background.ShouldBe(theme.LastMoveSquare);
    }

    [Fact]
    public void First_move_is_black_for_fen_games()
    {
        var state = new MatchViewState
        {
            Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            MoveSan = ["e5", "Nf3"],
            FirstMoveIsBlack = true,
        };

        var text = MatchScreen.RenderToText(state, 48, 16);

        text.ShouldContain("1. ... e5");
        text.ShouldContain("2. Nf3");
    }
}
