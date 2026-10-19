using Rooksh.Chess;
using Rooksh.Cli;
using Rooksh.Configuration;
using Rooksh.Match;
using Rooksh.Uci;

namespace Rooksh.Commands;

/// <summary>
/// M1 exit-criterion command: plays a text-only engine-vs-engine game and prints the PGN.
/// No renderer is involved, which keeps the engine layer independently exercisable.
/// </summary>
public static class EngineMatchCommand
{
    public static async Task<int> RunAsync(
        RookshConfig config,
        ParsedCommandLine cli,
        CancellationToken cancellationToken)
    {
        var verbose = cli.HasFlag("verbose") || cli.HasFlag("debug");
        var trace = verbose
            ? (Action<string, string>)((slot, line) => Console.Error.WriteLine($"[{slot}] {line}"))
            : null;

        var launcher = new ProcessEngineLauncher((path, line) => Console.Error.WriteLine($"[{path}] {line}"));

        var whiteOptions = MatchSetup.ToEngineOptions(config.EngineA, "a", config);
        var blackOptions = MatchSetup.ToEngineOptions(config.EngineB, "b", config);

        var startFen = cli.GetFlag("fen");
        if (startFen is null && cli.Positionals.Count > 0)
        {
            startFen = cli.Positionals[0];
        }

        IChessGame game = GeraChessGameFactory.Instance.Create(startFen);

        await using var whiteSession = new EngineSession("A", whiteOptions, launcher, trace);
        await using var blackSession = new EngineSession("B", blackOptions, launcher, trace);

        await whiteSession.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await blackSession.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await whiteSession.NewGameAsync(cancellationToken).ConfigureAwait(false);
        await blackSession.NewGameAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"A (white): {whiteSession.DisplayName}");
        Console.WriteLine($"B (black): {blackSession.DisplayName}");
        Console.WriteLine($"Position : {(game.IsStandardStart ? "startpos" : game.Fen)}");
        Console.WriteLine();

        var white = new EngineMoveSource(whiteSession);
        var black = new EngineMoveSource(blackSession);

        var options = new MatchOptions
        {
            Mode = MatchMode.EngineVsEngine,
            TimeControl = MatchSetup.ToTimeControl(config.Match),
            Ponder = config.Match.Ponder,
            Depth = config.Match.Depth,
            Nodes = config.Match.Nodes,
        };

        var record = await MatchOrchestrator.PlayAsync(
            game,
            white,
            black,
            options,
            onEvent: evt => PrintEvent(evt, whiteSession.DisplayName, blackSession.DisplayName),
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine(record.ToPgn());
        return 0;
    }

    private static void PrintEvent(MatchEvent matchEvent, string whiteName, string blackName)
    {
        switch (matchEvent)
        {
            case MovePlayedEvent move:
                var number = ((move.Ply - 1) / 2) + 1;
                var prefix = move.Move.Side == ChessSide.White ? $"{number}." : $"{number}...";
                Console.WriteLine($"{prefix} {move.Move.San,-8} {FormatClock(move)}");
                break;

            case EngineFailedEvent failure:
                Console.WriteLine($"! {failure.Side}: {failure.Message}");
                break;

            case MatchEndedEvent ended:
                Console.WriteLine($"Result: {ended.Result} ({ended.Termination})");
                break;
        }
    }

    private static string FormatClock(MovePlayedEvent move)
    {
        static string Format(TimeSpan time) =>
            time.TotalHours >= 1
                ? time.ToString(@"h\:mm\:ss")
                : time.ToString(@"m\:ss");

        return $"[w {Format(move.WhiteRemaining)} b {Format(move.BlackRemaining)}]";
    }
}
