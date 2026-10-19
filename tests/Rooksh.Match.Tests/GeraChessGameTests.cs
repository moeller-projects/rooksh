using Rooksh.Chess;

namespace Rooksh.Match.Tests;

public class GeraChessGameTests
{
    private static IChessGame NewGame() => GeraChessGameFactory.Instance.Create();

    [Fact]
    public void Start_position_has_twenty_legal_moves()
    {
        var game = NewGame();

        game.SideToMove.ShouldBe(ChessSide.White);
        game.LegalMoves.Count.ShouldBe(20);
        game.IsStandardStart.ShouldBeTrue();
        game.LegalMoves.Select(m => m.Uci).ShouldContain("e2e4");
        game.LegalMoves.Select(m => m.San).ShouldContain("Nf3");
    }

    [Fact]
    public void Applying_a_move_updates_side_fen_and_history()
    {
        var game = NewGame();

        var move = game.Apply("e2e4");

        move.Uci.ShouldBe("e2e4");
        move.San.ShouldBe("e4");
        move.Side.ShouldBe(ChessSide.White);
        game.SideToMove.ShouldBe(ChessSide.Black);
        game.Fen.ShouldStartWith("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b");
        game.History.Count.ShouldBe(1);
        game.UciHistory.ShouldBe(["e2e4"]);
    }

    [Fact]
    public void Resolves_san_and_uci_input()
    {
        var game = NewGame();

        game.Resolve("Nf3")!.Uci.ShouldBe("g1f3");
        game.Resolve("g1f3")!.San.ShouldBe("Nf3");
        game.Resolve("e2e5").ShouldBeNull();
        game.Resolve("Qh5").ShouldBeNull();
    }

    [Fact]
    public void Illegal_move_throws()
    {
        var game = NewGame();

        Should.Throw<ChessRuleException>(() => game.Apply("e2e5"));
    }

    [Fact]
    public void Detects_checkmate_and_winner()
    {
        var game = GeraChessGameFactory.Instance.Create("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1");

        game.Apply("f7g7");

        game.Status.Kind.ShouldBe(GameEndKind.Checkmate);
        game.Status.Winner.ShouldBe(ChessSide.White);
        game.Status.Result.ShouldBe("1-0");
        game.Status.IsOver.ShouldBeTrue();
    }

    [Fact]
    public void Detects_stalemate_as_a_draw()
    {
        var game = GeraChessGameFactory.Instance.Create("k7/8/1Q6/8/8/8/8/7K b - - 0 1");

        game.Status.Kind.ShouldBe(GameEndKind.Stalemate);
        game.Status.IsDraw.ShouldBeTrue();
        game.Status.Result.ShouldBe("1/2-1/2");
        game.LegalMoves.ShouldBeEmpty();
    }

    [Fact]
    public void Detects_insufficient_material()
    {
        var game = GeraChessGameFactory.Instance.Create("4k3/8/8/8/8/8/8/4K3 w - - 0 1");

        game.Status.Kind.ShouldBe(GameEndKind.InsufficientMaterial);
        game.Status.IsDraw.ShouldBeTrue();
    }

    [Fact]
    public void Handles_promotion_moves()
    {
        var game = GeraChessGameFactory.Instance.Create("8/P7/8/8/8/8/7p/4K2k w - - 0 1");

        var promos = game.LegalMoves.Where(m => m.Uci.StartsWith("a7a8")).Select(m => m.Uci).ToList();
        promos.ShouldContain("a7a8q");
        promos.ShouldContain("a7a8n");

        game.Apply("a7a8q");
        game.Fen.ShouldStartWith("Q7");
    }

    [Fact]
    public void Undo_last_restores_the_position()
    {
        var game = NewGame();
        var start = game.Fen;

        game.Apply("e2e4").Uci.ShouldBe("e2e4");
        game.UndoLast().ShouldBeTrue();

        game.Fen.ShouldBe(start);
        game.History.ShouldBeEmpty();
        game.UndoLast().ShouldBeFalse();
    }

    [Fact]
    public void Nonstandard_start_is_not_flagged_as_standard()
    {
        var game = GeraChessGameFactory.Instance.Create("8/8/8/4k3/8/8/4K3/8 w - - 0 1");

        game.IsStandardStart.ShouldBeFalse();
        game.StartFen.ShouldBe("8/8/8/4k3/8/8/4K3/8 w - - 0 1");
    }
}
