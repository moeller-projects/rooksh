using Rooksh.Uci;
using Shouldly;

namespace Rooksh.Uci.Tests;

public class UciGoParametersTests
{
    [Fact]
    public void Renders_clock_search()
    {
        var go = new UciGoParameters
        {
            WhiteTimeMs = 600_000,
            BlackTimeMs = 590_000,
            WhiteIncrementMs = 2000,
            BlackIncrementMs = 2000,
        };

        go.ToCommand().ShouldBe("go wtime 600000 btime 590000 winc 2000 binc 2000");
    }

    [Fact]
    public void Renders_infinite_when_nothing_set()
    {
        new UciGoParameters().ToCommand().ShouldBe("go infinite");
    }

    [Fact]
    public void Renders_depth_nodes_and_movetime()
    {
        new UciGoParameters { Depth = 18 }.ToCommand().ShouldBe("go depth 18");
        new UciGoParameters { Nodes = 1_000_000 }.ToCommand().ShouldBe("go nodes 1000000");
        new UciGoParameters { MoveTimeMs = 500 }.ToCommand().ShouldBe("go movetime 500");
    }

    [Fact]
    public void Renders_ponder_and_searchmoves()
    {
        var go = new UciGoParameters
        {
            Ponder = true,
            WhiteTimeMs = 1000,
            BlackTimeMs = 2000,
            SearchMoves = ["e2e4", "d2d4"],
        };

        go.ToCommand().ShouldBe("go searchmoves e2e4 d2d4 ponder wtime 1000 btime 2000");
    }

    [Fact]
    public void Renders_movestogo_and_mate()
    {
        var go = new UciGoParameters { MovesToGo = 40, Mate = 2 };
        go.ToCommand().ShouldBe("go movestogo 40 mate 2");
    }
}
