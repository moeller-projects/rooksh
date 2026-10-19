using Rooksh.Chess;

namespace Rooksh.Match.Tests;

public class PgnSerializerTests
{
    private static ChessMove White(string uci, string san) => new(uci, san, ChessSide.White);

    private static ChessMove Black(string uci, string san) => new(uci, san, ChessSide.Black);

    [Fact]
    public void Writes_headers_and_movetext()
    {
        var record = new GameRecord
        {
            WhiteName = "Alice",
            BlackName = "Bob",
            EventName = "Test Event",
            Site = "Local",
            Date = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
            TimeControl = TimeControl.WithIncrement(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5)),
        };

        record.Moves.Add(White("e2e4", "e4"));
        record.Moves.Add(Black("e7e5", "e5"));
        record.Moves.Add(White("g1f3", "Nf3"));
        record.Status = new GameStatus(GameEndKind.Checkmate, ChessSide.White);

        var pgn = record.ToPgn();

        pgn.ShouldContain("[Event \"Test Event\"]");
        pgn.ShouldContain("[Date \"2026.10.19\"]");
        pgn.ShouldContain("[White \"Alice\"]");
        pgn.ShouldContain("[Black \"Bob\"]");
        pgn.ShouldContain("[Result \"1-0\"]");
        pgn.ShouldContain("[TimeControl \"600+5\"]");
        pgn.ShouldContain("[Termination \"checkmate\"]");
        pgn.ShouldContain("1. e4 e5 2. Nf3 1-0");
        pgn.ShouldNotContain("[FEN");
    }

    [Fact]
    public void Writes_fen_and_setup_for_nonstandard_starts()
    {
        const string fen = "8/8/8/4k3/8/8/4K3/8 w - - 0 1";
        var record = new GameRecord { StartFen = fen };
        record.Status = GameStatus.Ongoing;

        var pgn = record.ToPgn();

        pgn.ShouldContain("[SetUp \"1\"]");
        pgn.ShouldContain($"[FEN \"{fen}\"]");
        pgn.ShouldContain("[Result \"*\"]");
    }

    [Fact]
    public void Numbers_a_game_that_starts_with_black()
    {
        var moves = new List<ChessMove>
        {
            Black("e7e5", "e5"),
            White("g1f3", "Nf3"),
        };

        var movetext = PgnSerializer.WriteMoveText(moves, "0-1");

        movetext.ShouldBe("1... e5 2. Nf3 0-1\n");
    }

    [Fact]
    public void Draw_result_is_written()
    {
        var record = new GameRecord();
        record.Moves.Add(White("e2e4", "e4"));
        record.Status = new GameStatus(GameEndKind.Stalemate, null);

        var pgn = record.ToPgn();

        pgn.ShouldContain("[Result \"1/2-1/2\"]");
        pgn.ShouldContain("[Termination \"stalemate\"]");
        pgn.ShouldContain("1. e4 1/2-1/2");
    }

    [Fact]
    public void Wraps_long_movetext_at_the_configured_column()
    {
        var moves = new List<ChessMove>();
        for (var i = 0; i < 40; i++)
        {
            moves.Add(White($"a2a3", "a3"));
            moves.Add(Black($"a7a6", "a6"));
        }

        var movetext = PgnSerializer.WriteMoveText(moves, "1-0", wrapColumn: 40);

        foreach (var line in movetext.TrimEnd('\n').Split('\n'))
        {
            line.Length.ShouldBeLessThanOrEqualTo(40);
        }

        movetext.ShouldContain("\n");
    }

    [Fact]
    public void Escapes_quotes_in_headers()
    {
        var record = new GameRecord { WhiteName = "He said \"hi\"" };
        record.Status = GameStatus.Ongoing;

        record.ToPgn().ShouldContain("[White \"He said \\\"hi\\\"\"]");
    }
}
