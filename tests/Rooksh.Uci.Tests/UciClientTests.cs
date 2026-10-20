using Rooksh.TestSupport;
using Rooksh.Uci;
using Shouldly;

namespace Rooksh.Uci.Tests;

public class UciClientTests
{
    [Fact]
    public async Task Handshake_reports_id_and_options()
    {
        await using var process = new FakeEngineProcess(new FakeEngineOptions { Name = "TestEngine" });
        await using var client = new UciClient(process, "test");

        var handshake = await client.InitializeAsync(TimeSpan.FromSeconds(5));

        handshake.Name.ShouldBe("TestEngine");
        handshake.Author.ShouldBe("rooksh-tests");
        handshake.SupportsPonder.ShouldBeTrue();
        handshake.SupportsMultiPv.ShouldBeTrue();
        client.Name.ShouldBe("TestEngine");
    }

    [Fact]
    public async Task ApplyOptions_is_recorded_by_engine()
    {
        await using var process = new FakeEngineProcess();
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        await client.ApplyOptionsAsync(
        [
            new KeyValuePair<string, string?>("Hash", "128"),
            new KeyValuePair<string, string?>("Threads", "4"),
            new KeyValuePair<string, string?>("Ponder", "true"),
        ]);

        process.SetOptions["Hash"].ShouldBe("128");
        process.SetOptions["Threads"].ShouldBe("4");
        process.SetOptions["Ponder"].ShouldBe("true");
    }

    [Fact]
    public async Task Position_and_go_produce_a_bestmove()
    {
        await using var process = new FakeEngineProcess(
            new FakeEngineOptions { MoveSelector = (_, legal) => legal.Contains("e2e4") ? "e2e4" : legal[0] });
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(null, []);
        client.Go(new UciGoParameters { WhiteTimeMs = 1000, BlackTimeMs = 1000 });

        var best = await client.WaitForBestMoveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        best.Move.ShouldBe("e2e4");
    }

    [Fact]
    public async Task Position_command_resends_full_move_list()
    {
        await using var process = new FakeEngineProcess();
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(null, ["e2e4", "e7e5", "g1f3"]);
        await client.IsReadyAsync();

        process.RecordedCommands.ShouldContain("position startpos moves e2e4 e7e5 g1f3");
    }

    [Fact]
    public async Task Position_command_uses_fen_when_start_is_nonstandard()
    {
        const string fen = "7k/5Q2/6K1/8/8/8/8/8 w - - 0 1";
        await using var process = new FakeEngineProcess();
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(fen, ["f7g7"]);
        await client.IsReadyAsync();

        process.RecordedCommands.ShouldContain($"position fen {fen} moves f7g7");
    }

    [Fact]
    public async Task Ponder_search_does_not_complete_until_ponderhit()
    {
        await using var process = new FakeEngineProcess(
            new FakeEngineOptions { PonderMove = "d2d4", MoveSelector = (_, legal) => legal.FirstOrDefault(m => m == "e7e5") ?? legal[0] });
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(null, ["e2e4"]);
        client.Go(new UciGoParameters { Ponder = true, WhiteTimeMs = 1000, BlackTimeMs = 1000 });

        var pending = client.WaitForBestMoveAsync();
        pending.IsCompleted.ShouldBeFalse();

        client.PonderHit();
        var best = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        best.Move.ShouldBe("e7e5");
        best.Ponder.ShouldBe("d2d4");
    }

    [Fact]
    public async Task Stop_completes_a_running_search()
    {
        await using var process = new FakeEngineProcess(new FakeEngineOptions { SearchDelay = TimeSpan.FromSeconds(30) });
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(null, []);
        client.Go(new UciGoParameters { WhiteTimeMs = 1000, BlackTimeMs = 1000 });

        var pending = client.WaitForBestMoveAsync();
        client.Stop();

        var best = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        best.Move.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Buffers_bestmove_that_arrives_before_await()
    {
        await using var process = new FakeEngineProcess();
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(null, []);
        client.Go(new UciGoParameters { Depth = 1 });

        // Give the engine a chance to answer before we start waiting.
        await Task.Delay(100);

        var best = await client.WaitForBestMoveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        best.Move.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Engine_death_faults_a_pending_bestmove()
    {
        await using var process = new FakeEngineProcess(new FakeEngineOptions { CrashAfterFirstMove = true });
        await using var client = new UciClient(process, "test");
        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(null, []);
        client.Go(new UciGoParameters { WhiteTimeMs = 1000, BlackTimeMs = 1000 });

        // First bestmove arrives, then the process dies.
        var first = await client.WaitForBestMoveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        first.Move.ShouldNotBeNullOrEmpty();

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!process.HasExited && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        process.HasExited.ShouldBeTrue();

        await Should.ThrowAsync<EngineProcessExitedException>(async () =>
            await client.IsReadyAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Info_events_are_forwarded_and_latest_is_cached()
    {
        var infos = new List<UciInfo>();
        await using var process = new FakeEngineProcess();
        await using var client = new UciClient(process, "test");
        client.InfoReceived += info => infos.Add(info);

        await client.InitializeAsync(TimeSpan.FromSeconds(5));

        client.SetPosition(null, []);
        client.Go(new UciGoParameters { Depth = 1 });
        await client.WaitForBestMoveAsync().WaitAsync(TimeSpan.FromSeconds(5));

        infos.ShouldNotBeEmpty();
        client.LatestInfo.Depth.ShouldBe(5);
        client.LatestInfo.Score.Cp.ShouldBe(25);
    }
}
