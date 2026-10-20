using Microsoft.Extensions.Configuration;
using Rooksh.Cli;
using Rooksh.Configuration;

namespace Rooksh.Tests;

public class ConfigLoaderTests
{
    private static ConfigSources Sources(
        Dictionary<string, string?>? toml = null,
        Dictionary<string, string?>? env = null,
        Dictionary<string, string?>? flags = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(toml ?? [])
            .Build();

        return new ConfigSources
        {
            Toml = configuration,
            Environment = env ?? [],
            Flags = flags ?? [],
        };
    }

    [Fact]
    public void Defaults_are_applied_without_any_source()
    {
        var config = ConfigLoader.Resolve(Sources());

        config.EngineA.Path.ShouldBeNull();
        config.Match.Mode.ShouldBe("engine_vs_engine");
        config.Match.TimeControl.ShouldBe("increment");
        config.Match.BaseMinutes.ShouldBe(10);
        config.Match.IncrementSeconds.ShouldBe(2);
        config.Match.Ponder.ShouldBeTrue();
        config.Ui.Ascii.ShouldBeFalse();
        config.Ui.ShowCoordinates.ShouldBeTrue();
        config.Ui.MultiPv.ShouldBe(3);
    }

    [Fact]
    public void Toml_values_are_read_using_canonical_keys()
    {
        var config = ConfigLoader.Resolve(Sources(toml: new Dictionary<string, string?>
        {
            ["engine_a:path"] = "engines/zwischenzug",
            ["engine_a:hash_mb"] = "64",
            ["engine_a:threads"] = "1",
            ["engine_a:args:0"] = "--foo",
            ["engine_a:args:1"] = "--bar",
            ["engine_b:path"] = "engines/stockfish",
            ["engine_b:hash_mb"] = "128",
            ["engine_b:threads"] = "4",
            ["match:mode"] = "human_vs_engine",
            ["match:time_control"] = "sudden_death",
            ["match:base_minutes"] = "5",
            ["match:increment_seconds"] = "0",
            ["match:ponder"] = "false",
            ["match:human_side"] = "b",
            ["ui:ascii"] = "true",
            ["ui:info_refresh_ms"] = "25",
            ["ui:multipv"] = "5",
        }));

        config.EngineA.Path.ShouldBe("engines/zwischenzug");
        config.EngineA.HashMb.ShouldBe(64);
        config.EngineA.Threads.ShouldBe(1);
        config.EngineA.Args.ShouldBe(["--foo", "--bar"]);
        config.EngineB.Path.ShouldBe("engines/stockfish");
        config.EngineB.HashMb.ShouldBe(128);
        config.Match.Mode.ShouldBe("human_vs_engine");
        config.Match.TimeControl.ShouldBe("sudden_death");
        config.Match.BaseMinutes.ShouldBe(5);
        config.Match.Ponder.ShouldBeFalse();
        config.Match.HumanSide.ShouldBe("b");
        config.Ui.Ascii.ShouldBeTrue();
        config.Ui.InfoRefreshMs.ShouldBe(25);
        config.Ui.MultiPv.ShouldBe(5);
    }

    [Fact]
    public void Environment_variables_use_the_canonical_names()
    {
        var config = ConfigLoader.Resolve(Sources(env: new Dictionary<string, string?>
        {
            ["ROOKSH_ENGINE_A_PATH"] = "/usr/bin/engine",
            ["ROOKSH_BASE_MINUTES"] = "7",
            ["ROOKSH_INCREMENT_SECONDS"] = "3",
            ["ROOKSH_PONDER"] = "false",
            ["ROOKSH_ASCII"] = "1",
            ["ROOKSH_ENGINE_A_ARGS"] = "a b,c",
        }));

        config.EngineA.Path.ShouldBe("/usr/bin/engine");
        config.Match.BaseMinutes.ShouldBe(7);
        config.Match.IncrementSeconds.ShouldBe(3);
        config.Match.Ponder.ShouldBeFalse();
        config.Ui.Ascii.ShouldBeTrue();
        config.EngineA.Args.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Command_line_flags_win_over_environment_and_toml()
    {
        var config = ConfigLoader.Resolve(Sources(
            toml: new Dictionary<string, string?> { ["match:base_minutes"] = "1" },
            env: new Dictionary<string, string?> { ["ROOKSH_BASE_MINUTES"] = "2" },
            flags: new Dictionary<string, string?> { ["base-minutes"] = "3" }));

        config.Match.BaseMinutes.ShouldBe(3);
    }

    [Fact]
    public void Environment_wins_over_toml()
    {
        var config = ConfigLoader.Resolve(Sources(
            toml: new Dictionary<string, string?> { ["match:base_minutes"] = "1" },
            env: new Dictionary<string, string?> { ["ROOKSH_BASE_MINUTES"] = "2" }));

        config.Match.BaseMinutes.ShouldBe(2);
    }

    [Fact]
    public void Load_reads_a_toml_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rooksh-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "rooksh.toml");
        File.WriteAllText(
            path,
            """
            [engine_a]
            path = "engines/zwischenzug"

            [match]
            base_minutes = 3
            increment_seconds = 1
            ponder = false
            """);

        try
        {
            var (cli, config, tomlPath) = ConfigLoader.Load([], environment: new Dictionary<string, string?>(), workingDirectory: directory);

            cli.Command.ShouldBeEmpty();
            tomlPath.ShouldBe(path);
            config.EngineA.Path.ShouldBe("engines/zwischenzug");
            config.Match.BaseMinutes.ShouldBe(3);
            config.Match.IncrementSeconds.ShouldBe(1);
            config.Match.Ponder.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
