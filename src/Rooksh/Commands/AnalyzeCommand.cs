using System.Globalization;
using Rooksh.Cli;
using Rooksh.Configuration;
using Rooksh.Match;
using Rooksh.Uci;

namespace Rooksh.Commands;

/// <summary>Analysis mode: one engine, MultiPV, live info stream, optional infinite search.</summary>
public static class AnalyzeCommand
{
    public static async Task<int> RunAsync(
        RookshConfig config,
        ParsedCommandLine cli,
        CancellationToken cancellationToken)
    {
        var fen = cli.GetFlag("fen");
        if (fen is null && cli.Positionals.Count > 0)
        {
            fen = cli.Positionals[0];
        }

        var launcher = new ProcessEngineLauncher((path, line) => Console.Error.WriteLine($"[{path}] {line}"));
        var options = MatchSetup.ToEngineOptions(config.EngineA, "a", config);

        await using var session = new EngineSession("A", options, launcher);
        await session.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var multiPv = cli.GetFlag("multipv") is { } mp && int.TryParse(mp, out var parsed) ? parsed : config.Ui.MultiPv;
        if (session.Handshake?.SupportsMultiPv == true)
        {
            await session.SetOptionAsync("MultiPV", multiPv.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        }

        Console.WriteLine($"Engine  : {session.DisplayName}");
        Console.WriteLine($"Position: {(string.IsNullOrWhiteSpace(fen) ? "startpos" : fen)}");
        Console.WriteLine($"MultiPV : {multiPv}");
        Console.WriteLine();

        var infinite = cli.HasFlag("infinite") || (config.Match.Depth is null && config.Match.Nodes is null && cli.GetFlag("depth") is null && cli.GetFlag("nodes") is null && cli.GetFlag("movetime") is null);

        var go = new UciGoParameters
        {
            Depth = GetInt(cli, "depth") ?? config.Match.Depth ?? (infinite ? null : 18),
            Nodes = GetLong(cli, "nodes") ?? config.Match.Nodes,
            MoveTimeMs = GetInt(cli, "movetime"),
            Infinite = infinite,
        };

        var printedDepths = new HashSet<int>();
        session.InfoUpdated += info => PrintInfo(info, printedDepths);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            linked.Cancel();
        };

        session.StartSearch(string.IsNullOrWhiteSpace(fen) ? null : fen, [], go);

        if (infinite)
        {
            Console.WriteLine("(infinite search — press Ctrl+C to stop)");
            try
            {
                await Task.Delay(Timeout.Infinite, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected stop
            }

            session.Stop();
        }

        UciBestMove best;
        try
        {
            best = await session.AwaitBestMoveAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            session.Stop();
            best = await session.AwaitBestMoveAsync(CancellationToken.None).ConfigureAwait(false);
        }

        Console.WriteLine();
        Console.WriteLine($"bestmove: {best.Move ?? "(none)"}{(best.HasPonder ? $" ponder {best.Ponder}" : string.Empty)}");
        return 0;
    }

    private static void PrintInfo(UciInfo info, HashSet<int> printedDepths)
    {
        if (info.IsEmpty || info.Depth is null)
        {
            return;
        }

        if (!printedDepths.Add(info.Depth.Value))
        {
            return;
        }

        var pv = info.Pv.Count > 0 ? string.Join(' ', info.Pv) : string.Empty;
        var timeMs = info.TimeMs ?? 0;
        Console.WriteLine($"depth {info.Depth,2}  {info.Score.ToDisplayString(),-7}  {timeMs,7}ms  {pv}");
    }

    private static int? GetInt(ParsedCommandLine cli, string flag) =>
        cli.GetFlag(flag) is { } value && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static long? GetLong(ParsedCommandLine cli, string flag) =>
        cli.GetFlag(flag) is { } value && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
