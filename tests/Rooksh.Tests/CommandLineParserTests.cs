using Rooksh.Cli;

namespace Rooksh.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void Parses_command_flags_and_positionals()
    {
        var cli = CommandLineParser.Parse(["engine-match", "--engine-a-path", "engines/a", "--depth", "5"]);

        cli.Command.ShouldBe("engine-match");
        cli.GetFlag("engine-a-path").ShouldBe("engines/a");
        cli.GetFlag("depth").ShouldBe("5");
    }

    [Fact]
    public void Supports_equals_form()
    {
        var cli = CommandLineParser.Parse(["play", "--config=rooksh.toml"]);

        cli.GetFlag("config").ShouldBe("rooksh.toml");
    }

    [Fact]
    public void Boolean_flags_do_not_swallow_the_next_token()
    {
        var cli = CommandLineParser.Parse(["play", "--ascii", "extra"]);

        cli.GetFlag("ascii").ShouldBe("true");
        cli.Positionals.ShouldBe(["extra"]);
    }

    [Fact]
    public void Boolean_flags_accept_explicit_values()
    {
        CommandLineParser.Parse(["--ascii=false"]).GetFlag("ascii").ShouldBe("false");
        CommandLineParser.Parse(["--ponder", "--no-ponder"]).GetFlag("ponder").ShouldBe("false");
        CommandLineParser.Parse(["--no-ascii"]).GetFlag("ascii").ShouldBe("false");
        CommandLineParser.Parse(["--no-coordinates"]).GetFlag("coordinates").ShouldBe("false");
    }

    [Fact]
    public void Collects_fen_positional()
    {
        const string fen = "7k/5Q2/6K1/8/8/8/8/8 w - - 0 1";
        var cli = CommandLineParser.Parse(["analyze", fen]);

        cli.Command.ShouldBe("analyze");
        cli.Positionals.ShouldBe([fen]);
    }
}
