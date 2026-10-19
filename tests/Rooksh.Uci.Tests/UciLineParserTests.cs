using Rooksh.Uci;
using Shouldly;

namespace Rooksh.Uci.Tests;

public class UciLineParserTests
{
    [Fact]
    public void Parses_depth_score_nodes_and_pv()
    {
        UciLineParser.TryParseInfo(
            "info depth 14 seldepth 21 score cp 38 nodes 1200000 nps 1200000 time 1000 pv Nf3 Nc6 Bb5 a6",
            out var info).ShouldBeTrue();

        info.Depth.ShouldBe(14);
        info.SelDepth.ShouldBe(21);
        info.Score.Cp.ShouldBe(38);
        info.Score.Mate.ShouldBeNull();
        info.Nodes.ShouldBe(1_200_000);
        info.Nps.ShouldBe(1_200_000);
        info.TimeMs.ShouldBe(1000);
        info.Pv.ShouldBe(["Nf3", "Nc6", "Bb5", "a6"]);
    }

    [Fact]
    public void Parses_mate_scores_and_bounds()
    {
        UciLineParser.TryParseInfo("info depth 30 score mate 3", out var mateIn).ShouldBeTrue();
        mateIn.Score.Mate.ShouldBe(3);
        mateIn.Score.Bound.ShouldBe(UciScoreBound.Exact);

        UciLineParser.TryParseInfo("info depth 5 score cp -120 upperbound", out var upper).ShouldBeTrue();
        upper.Score.Cp.ShouldBe(-120);
        upper.Score.Bound.ShouldBe(UciScoreBound.UpperBound);

        UciLineParser.TryParseInfo("info score mate -2 lowerbound", out var mated).ShouldBeTrue();
        mated.Score.Mate.ShouldBe(-2);
        mated.Score.Bound.ShouldBe(UciScoreBound.LowerBound);
    }

    [Fact]
    public void Parses_string_info_and_ignores_unknown_tokens()
    {
        UciLineParser.TryParseInfo("info depth 1 xyzzy 7 string hello engine world", out var info).ShouldBeTrue();

        info.Depth.ShouldBe(1);
        info.Text.ShouldBe("hello engine world");
    }

    [Fact]
    public void Parses_bestmove_with_and_without_ponder()
    {
        UciLineParser.TryParseBestMove("bestmove e7e5 ponder d2d4", out var withPonder).ShouldBeTrue();
        withPonder.Move.ShouldBe("e7e5");
        withPonder.Ponder.ShouldBe("d2d4");
        withPonder.HasPonder.ShouldBeTrue();

        UciLineParser.TryParseBestMove("bestmove g1f3", out var plain).ShouldBeTrue();
        plain.Move.ShouldBe("g1f3");
        plain.HasPonder.ShouldBeFalse();
    }

    [Fact]
    public void Parses_bestmove_none()
    {
        UciLineParser.TryParseBestMove("bestmove (none)", out var none).ShouldBeTrue();
        none.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void Parses_spin_option_with_bounds()
    {
        UciLineParser.TryParseOption(
            "option name Hash type spin default 16 min 1 max 4096",
            out var option).ShouldBeTrue();

        option.Name.ShouldBe("Hash");
        option.Type.ShouldBe(UciOptionType.Spin);
        option.Default.ShouldBe("16");
        option.Min.ShouldBe(1);
        option.Max.ShouldBe(4096);
    }

    [Fact]
    public void Parses_combo_option_with_variants()
    {
        UciLineParser.TryParseOption(
            "option name Style type combo default Normal var Solid var Normal var Risky",
            out var option).ShouldBeTrue();

        option.Name.ShouldBe("Style");
        option.Type.ShouldBe(UciOptionType.Combo);
        option.Default.ShouldBe("Normal");
        option.Values.ShouldBe(["Solid", "Normal", "Risky"]);
    }

    [Fact]
    public void Parses_multi_word_option_name()
    {
        UciLineParser.TryParseOption(
            "option name Move Overhead type spin default 10 min 0 max 5000",
            out var option).ShouldBeTrue();

        option.Name.ShouldBe("Move Overhead");
        option.Type.ShouldBe(UciOptionType.Spin);
        option.Default.ShouldBe("10");
    }

    [Fact]
    public void Parses_button_and_check_options()
    {
        UciLineParser.TryParseOption("option name Clear Hash type button", out var button).ShouldBeTrue();
        button.Type.ShouldBe(UciOptionType.Button);

        UciLineParser.TryParseOption("option name Ponder type check default false", out var check).ShouldBeTrue();
        check.Type.ShouldBe(UciOptionType.Check);
        check.Default.ShouldBe("false");
    }

    [Fact]
    public void Parses_id_lines()
    {
        UciLineParser.TryParseId("id name Stockfish 17", out var name).ShouldBeTrue();
        name.Name.ShouldBe("Stockfish 17");

        UciLineParser.TryParseId("id author the Stockfish developers", out var author).ShouldBeTrue();
        author.Author.ShouldBe("the Stockfish developers");
    }

    [Fact]
    public void Dispatches_known_lines_to_typed_events()
    {
        UciLineParser.Parse("uciok").ShouldBeOfType<UciOkEvent>();
        UciLineParser.Parse("readyok").ShouldBeOfType<UciReadyOkEvent>();
        UciLineParser.Parse("bestmove e2e4").ShouldBeOfType<UciBestMoveEvent>();
        UciLineParser.Parse("info depth 3 score cp 10").ShouldBeOfType<UciInfoEvent>();
        UciLineParser.Parse("id name X").ShouldBeOfType<UciIdEvent>();
        UciLineParser.Parse("option name Hash type spin").ShouldBeOfType<UciOptionEvent>();
        UciLineParser.Parse("copyprotection ok").ShouldBeOfType<UciRawLineEvent>();
    }

    [Fact]
    public void Score_display_formats_centipawns_and_mates()
    {
        new UciScore(38, null).ToDisplayString().ShouldBe("+0.38");
        new UciScore(-455, null).ToDisplayString().ShouldBe("-4.55");
        new UciScore(null, 3).ToDisplayString().ShouldBe("#3");
        new UciScore(null, -2).ToDisplayString().ShouldBe("#-2");
    }
}
