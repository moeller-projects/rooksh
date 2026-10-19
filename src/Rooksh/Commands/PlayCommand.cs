using Rooksh.Chess;
using Rooksh.Cli;
using Rooksh.Configuration;
using Rooksh.Match;
using Rooksh.Rendering;
using Rooksh.Uci;

namespace Rooksh.Commands;

/// <summary>Interactive match per configuration: engine-vs-engine, human-vs-engine or human-vs-human.</summary>
public static class PlayCommand
{
    public static async Task<int> RunAsync(
        RookshConfig config,
        ParsedCommandLine cli,
        CancellationToken cancellationToken)
    {
        var mode = MatchSetup.ToMatchMode(config.Match.Mode);
        if (mode == MatchMode.Analysis)
        {
            return await AnalyzeCommand.RunAsync(config, cli, cancellationToken).ConfigureAwait(false);
        }

        var launcher = new ProcessEngineLauncher((path, line) => Console.Error.WriteLine($"[{path}] {line}"));
        var startFen = cli.GetFlag("fen") ?? (cli.Positionals.Count > 0 ? cli.Positionals[0] : null);
        IChessGame game = GeraChessGameFactory.Instance.Create(startFen);

        var humanSide = config.Match.HumanSide.ToLowerInvariant();
        var humanIsWhite = mode == MatchMode.HumanVsHuman || (mode == MatchMode.HumanVsEngine && humanSide == "a");
        var humanIsBlack = mode == MatchMode.HumanVsHuman || (mode == MatchMode.HumanVsEngine && humanSide == "b");

        using var terminal = new ConsoleTerminal();
        var interactive = terminal.IsInteractive && !cli.HasFlag("no-tui") && !cli.HasFlag("ascii-no-tui");
        AnsiRenderer? renderer = null;
        TuiHost? host = null;

        if (interactive)
        {
            terminal.Enter();
            renderer = new AnsiRenderer(Console.Out, trueColor: true, mouse: true);
            renderer.Initialize();
            host = new TuiHost(renderer, terminal, config.Ui.Ascii);
        }

        EngineSession? aSession = null;
        EngineSession? bSession = null;

        try
        {
            IMoveSource white;
            if (humanIsWhite)
            {
                white = host is not null ? new TuiHumanMoveSource(host) : new ConsoleMoveSource("Human");
            }
            else
            {
                aSession = new EngineSession("A", MatchSetup.ToEngineOptions(config.EngineA, "a", config), launcher);
                await aSession.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await aSession.NewGameAsync(cancellationToken).ConfigureAwait(false);
                white = new EngineMoveSource(aSession);
            }

            IMoveSource black;
            if (humanIsBlack)
            {
                black = host is not null ? new TuiHumanMoveSource(host) : new ConsoleMoveSource("Human");
            }
            else
            {
                bSession = new EngineSession("B", MatchSetup.ToEngineOptions(config.EngineB, "b", config), launcher);
                await bSession.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await bSession.NewGameAsync(cancellationToken).ConfigureAwait(false);
                black = new EngineMoveSource(bSession);
            }

            var options = new MatchOptions
            {
                Mode = mode,
                TimeControl = MatchSetup.ToTimeControl(config.Match),
                Ponder = config.Match.Ponder,
                Depth = config.Match.Depth,
                Nodes = config.Match.Nodes,
            };

            var presenter = new TextMatchPresenter();
            Action<MatchEvent> onEvent = host is not null ? host.OnEvent : presenter.OnEvent;

            var record = await MatchOrchestrator.PlayAsync(
                game,
                white,
                black,
                options,
                onEvent,
                cancellationToken).ConfigureAwait(false);

            if (host is not null)
            {
                host.ShowFinished(record, cancellationToken);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(record.ToPgn());
            }

            return 0;
        }
        finally
        {
            if (aSession is not null)
            {
                await aSession.DisposeAsync().ConfigureAwait(false);
            }

            if (bSession is not null)
            {
                await bSession.DisposeAsync().ConfigureAwait(false);
            }

            host?.Dispose();
            renderer?.Dispose();
        }
    }
}
