using System.Reflection;
using Rooksh.Cli;
using Rooksh.Commands;
using Rooksh.Configuration;

return await RookshProgram.RunAsync(args);

internal static class RookshProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        ParsedCommandLine cli;
        RookshConfig config;

        try
        {
            (cli, config, _) = ConfigLoader.Load(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 2;
        }

        if (cli.HasFlag("version") || string.Equals(cli.Command, "version", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"rooksh {Assembly.GetExecutingAssembly().GetName().Version}");
            return 0;
        }

        if (cli.Command.Length == 0 || cli.HasFlag("help") || string.Equals(cli.Command, "help", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage();
            return cli.Command.Length == 0 ? 1 : 0;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            return cli.Command switch
            {
                "play" => await PlayCommand.RunAsync(config, cli, cancellation.Token).ConfigureAwait(false),
                "engine-match" => await EngineMatchCommand.RunAsync(config, cli, cancellation.Token).ConfigureAwait(false),
                "analyze" => await AnalyzeCommand.RunAsync(config, cli, cancellation.Token).ConfigureAwait(false),
                _ => UnknownCommand(cli.Command),
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (cli.HasFlag("debug"))
            {
                Console.Error.WriteLine(ex);
            }

            return 1;
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            rooksh — terminal chess GUI (engine-agnostic)

            Usage:
              rooksh play               Interactive match per configuration
              rooksh engine-match       Text-only engine vs engine, prints PGN
              rooksh analyze [fen]      Analysis mode against one engine

            Common options:
              --config <path>           TOML config file (default: rooksh.toml)
              --ascii                   ASCII piece fallback
              --engine-a-path <path>    Engine A binary
              --engine-b-path <path>    Engine B binary
              --mode <mode>             engine_vs_engine | human_vs_engine | human_vs_human | analysis
              --time-control <mode>     fixed | increment | sudden_death
              --base-minutes <n>        Base time in minutes
              --increment-seconds <n>   Increment in seconds
              --depth <n>               Fixed search depth
              --nodes <n>               Fixed node limit
              --fen <fen>               Start from a FEN
              --help                    Show this help
              --version                 Show version

            Every option also has an environment variable and TOML key using the same
            canonical name, e.g. --base-minutes / ROOKSH_BASE_MINUTES / [match] base_minutes.
            """);
    }
}
