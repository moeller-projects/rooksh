using Microsoft.Extensions.Time.Testing;
using Rooksh.Chess;
using Rooksh.TestSupport;
using Rooksh.Uci;

namespace Rooksh.Match.Tests;

public class MatchOrchestratorTests
{
    [Fact]
    public async Task Engine_vs_engine_plays_to_checkmate_and_records_pgn()
    {
        var launcher = new FakeEngineLauncher(new FakeEngineOptions
        {
            EmitInfo = false,
            MoveSelector = (_, legal) => legal.Contains("f7g7") ? "f7g7" : legal.FirstOrDefault(),
        });

        await using var white = new EngineSession("A", new UciEngineOptions { Path = "fake-a" }, launcher);
        await using var black = new EngineSession("B", new UciEngineOptions { Path = "fake-b" }, launcher);
        await white.InitializeAsync();
        await black.InitializeAsync();

        var game = GeraChessGameFactory.Instance.Create("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1");
        var events = new List<MatchEvent>();

        var record = await MatchOrchestrator.PlayAsync(
            game,
            new EngineMoveSource(white),
            new EngineMoveSource(black),
            new MatchOptions { TimeControl = TimeControl.SuddenDeath(TimeSpan.FromMinutes(5)) },
            events.Add);

        record.Status.Kind.ShouldBe(GameEndKind.Checkmate);
        record.Status.Winner.ShouldBe(ChessSide.White);
        record.Moves.Count.ShouldBe(1);
        record.Moves[0].San.ShouldBe("Qg7#");
        record.ToPgn().ShouldContain("1. Qg7# 1-0");

        events.OfType<MovePlayedEvent>().Count().ShouldBe(1);
        events.OfType<MatchEndedEvent>().Single().Result.ShouldBe("1-0");
    }

    [Fact]
    public async Task Engine_vs_engine_plays_a_full_game_to_a_terminal_result()
    {
        var launcher = new FakeEngineLauncher(new FakeEngineOptions
        {
            EmitInfo = false,
            MoveSelector = (_, legal) => legal.FirstOrDefault(),
        });

        await using var white = new EngineSession("A", new UciEngineOptions { Path = "fake-a" }, launcher);
        await using var black = new EngineSession("B", new UciEngineOptions { Path = "fake-b" }, launcher);
        await white.InitializeAsync();
        await black.InitializeAsync();

        var game = GeraChessGameFactory.Instance.Create();
        var record = await MatchOrchestrator.PlayAsync(
            game,
            new EngineMoveSource(white),
            new EngineMoveSource(black),
            new MatchOptions { TimeControl = TimeControl.SuddenDeath(TimeSpan.FromMinutes(5)) });

        record.Status.IsOver.ShouldBeTrue();
        record.Moves.Count.ShouldBeGreaterThan(0);
        record.ToPgn().ShouldContain("1.");
        record.Status.Result.ShouldNotBe("*");
    }

    [Fact]
    public async Task Flags_a_side_that_overspends_the_clock()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var control = TimeControl.SuddenDeath(TimeSpan.FromSeconds(10));
        var clock = new MatchClock(control, time);

        var slow = new StubMoveSource("Slow", request =>
        {
            time.Advance(TimeSpan.FromSeconds(11));
            return MoveChoice.Of(request.LegalMoves[0].Uci);
        });
        var other = new StubMoveSource("Other", request => MoveChoice.Of(request.LegalMoves[0].Uci));

        var game = GeraChessGameFactory.Instance.Create();
        var record = await new MatchOrchestrator(game, slow, other, new MatchOptions { TimeControl = control }, clock).RunAsync();

        record.Status.Kind.ShouldBe(GameEndKind.Timeout);
        record.Status.Winner.ShouldBe(ChessSide.Black);
        record.Moves.ShouldBeEmpty();
    }

    [Fact]
    public async Task Forfeit_loses_the_game()
    {
        var resigner = new StubMoveSource("Resigner", _ => MoveChoice.Forfeit("resigned"));
        var other = new StubMoveSource("Other", request => MoveChoice.Of(request.LegalMoves[0].Uci));

        var game = GeraChessGameFactory.Instance.Create();
        var events = new List<MatchEvent>();
        var record = await MatchOrchestrator.PlayAsync(
            game,
            resigner,
            other,
            new MatchOptions(),
            events.Add);

        record.Status.Kind.ShouldBe(GameEndKind.Resignation);
        record.Status.Winner.ShouldBe(ChessSide.Black);
        events.OfType<EngineFailedEvent>().Single().Side.ShouldBe(ChessSide.White);
    }

    [Fact]
    public async Task Illegal_move_is_an_engine_error()
    {
        var illegal = new StubMoveSource("Illegal", _ => MoveChoice.Of("e2e5"));
        var other = new StubMoveSource("Other", request => MoveChoice.Of(request.LegalMoves[0].Uci));

        var game = GeraChessGameFactory.Instance.Create();
        var record = await MatchOrchestrator.PlayAsync(game, illegal, other, new MatchOptions());

        record.Status.Kind.ShouldBe(GameEndKind.EngineError);
        record.Status.Winner.ShouldBe(ChessSide.Black);
        record.Moves.ShouldBeEmpty();
    }

    [Fact]
    public async Task Engine_death_is_reported_as_an_error()
    {
        var dead = new StubMoveSource("Dead", _ => throw new EngineProcessExitedException("Dead", 1));
        var other = new StubMoveSource("Other", request => MoveChoice.Of(request.LegalMoves[0].Uci));

        var game = GeraChessGameFactory.Instance.Create();
        var record = await MatchOrchestrator.PlayAsync(game, dead, other, new MatchOptions());

        record.Status.Kind.ShouldBe(GameEndKind.EngineError);
        record.Status.Winner.ShouldBe(ChessSide.Black);
    }

    [Fact]
    public async Task Request_carries_clock_and_move_history()
    {
        MoveRequest? seen = null;
        var capture = new StubMoveSource("Capture", request =>
        {
            seen ??= request;
            return MoveChoice.Of(request.LegalMoves[0].Uci);
        });
        var other = new StubMoveSource("Other", request => MoveChoice.Of(request.LegalMoves[0].Uci));

        var game = GeraChessGameFactory.Instance.Create();
        await MatchOrchestrator.PlayAsync(game, capture, other, new MatchOptions
        {
            TimeControl = TimeControl.SuddenDeath(TimeSpan.FromSeconds(30)),
        });

        seen.ShouldNotBeNull();
        seen!.SideToMove.ShouldBe(ChessSide.White);
        seen.IsStandardStart.ShouldBeTrue();
        seen.UciMoves.ShouldBeEmpty();
        seen.WhiteTimeMs.ShouldNotBeNull();
        seen.BlackTimeMs.ShouldNotBeNull();
        seen.LegalMoves.Count.ShouldBe(20);
    }

    [Fact]
    public async Task Ponder_is_negotiated_and_a_guessed_reply_triggers_ponderhit()
    {
        var launcher = new FakeEngineLauncher(options => options.Path == "fake-a"
            ? new FakeEngineProcess(new FakeEngineOptions
            {
                EmitInfo = false,
                PonderMove = "e7e5",
                MoveSelector = (position, legal) => position.Moves.Count == 0 ? "e2e4" : legal.FirstOrDefault(),
            })
            : new FakeEngineProcess(new FakeEngineOptions
            {
                EmitInfo = false,
                MoveSelector = (_, legal) => legal.Contains("e7e5") ? "e7e5" : legal.FirstOrDefault(),
            }));

        await using var white = new EngineSession("A", new UciEngineOptions { Path = "fake-a", Ponder = true }, launcher);
        await using var black = new EngineSession("B", new UciEngineOptions { Path = "fake-b", Ponder = true }, launcher);
        await white.InitializeAsync();
        await black.InitializeAsync();

        white.SupportsPonder.ShouldBeTrue();

        var game = GeraChessGameFactory.Instance.Create();
        var record = await MatchOrchestrator.PlayAsync(
            game,
            new EngineMoveSource(white),
            new EngineMoveSource(black),
            new MatchOptions
            {
                TimeControl = TimeControl.SuddenDeath(TimeSpan.FromMinutes(5)),
                Ponder = true,
                MaxPlies = 3,
            });

        record.Moves.Count.ShouldBe(3);

        var whiteCommands = launcher.Processes[0].RecordedCommands;
        whiteCommands.ShouldContain(line => line.StartsWith("go", StringComparison.Ordinal) && line.Contains("ponder", StringComparison.Ordinal));
        whiteCommands.ShouldContain("ponderhit");
    }

    private sealed class StubMoveSource : IMoveSource
    {
        private readonly Func<MoveRequest, MoveChoice> _choose;

        public StubMoveSource(string name, Func<MoveRequest, MoveChoice> choose)
        {
            DisplayName = name;
            _choose = choose;
        }

        public string DisplayName { get; }

        public Task<MoveChoice> ChooseMoveAsync(MoveRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_choose(request));
    }
}
