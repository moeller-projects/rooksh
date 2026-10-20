using Rooksh.Chess;
using Rooksh.Match;
using Rooksh.Uci;

namespace Rooksh.ContractTests;

/// <summary>
/// Finds a Stockfish binary. Set <c>ROOKSH_STOCKFISH</c> to an absolute path, drop a binary in
/// <c>engines/</c>, or put <c>stockfish</c> on <c>PATH</c>. Tests that need it return early when
/// it is absent so CI stays green without a reference engine.
/// </summary>
internal static class Stockfish
{
    public static string? Locate()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ROOKSH_STOCKFISH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        var names = OperatingSystem.IsWindows()
            ? new[] { "stockfish.exe", "stockfish-windows-x86-64-avx2.exe", "stockfish-windows-x86-64.exe" }
            : new[] { "stockfish", "stockfish-ubuntu-x86-64-avx2" };

        foreach (var directory in CandidateDirectories())
        {
            foreach (var name in names)
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in names)
            {
                var path = Path.Combine(directory, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; depth < 5 && directory is not null; depth++)
        {
            yield return directory.FullName;
            yield return Path.Combine(directory.FullName, "engines");
            directory = directory.Parent;
        }
    }

    public static UciEngineOptions Options(string path) => new()
    {
        Path = path,
        HashMb = 16,
        Threads = 1,
        Ponder = true,
    };
}

[Trait("Category", "Contract")]
public class StockfishContractTests
{
    [Fact]
    public async Task Completes_the_uci_handshake()
    {
        var path = Stockfish.Locate();
        if (path is null)
        {
            return;
        }

        await using var client = new UciClient(new ProcessEngineLauncher().Launch(Stockfish.Options(path)), path);
        var handshake = await client.InitializeAsync(TimeSpan.FromSeconds(20));

        handshake.Name.ShouldNotBeNullOrWhiteSpace();
        handshake.Options.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Returns_a_legal_move_at_depth_five()
    {
        var path = Stockfish.Locate();
        if (path is null)
        {
            return;
        }

        await using var client = new UciClient(new ProcessEngineLauncher().Launch(Stockfish.Options(path)), path);
        await client.InitializeAsync(TimeSpan.FromSeconds(20));
        await client.NewGameAsync();

        var game = GeraChessGameFactory.Instance.Create();
        client.SetPosition(null, []);
        client.Go(new UciGoParameters { Depth = 5 });

        var best = await client.WaitForBestMoveAsync().WaitAsync(TimeSpan.FromSeconds(60));

        best.IsNone.ShouldBeFalse();
        game.Resolve(best.Move!).ShouldNotBeNull();
    }

    [Fact]
    public async Task Two_instances_play_several_legal_moves()
    {
        var path = Stockfish.Locate();
        if (path is null)
        {
            return;
        }

        var launcher = new ProcessEngineLauncher();
        await using var white = new EngineSession("A", Stockfish.Options(path), launcher);
        await using var black = new EngineSession("B", Stockfish.Options(path), launcher);
        await white.InitializeAsync();
        await black.InitializeAsync();

        var game = GeraChessGameFactory.Instance.Create();
        var record = await MatchOrchestrator.PlayAsync(
            game,
            new EngineMoveSource(white),
            new EngineMoveSource(black),
            new MatchOptions
            {
                TimeControl = TimeControl.SuddenDeath(TimeSpan.FromMinutes(5)),
                Depth = 4,
                MaxPlies = 6,
            });

        record.Moves.Count.ShouldBe(6);
        record.Moves.ShouldAllBe(move => move.Uci.Length >= 4);
        record.Status.IsOver.ShouldBeFalse();
    }
}
