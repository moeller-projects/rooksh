using Rooksh.Configuration;
using Rooksh.Match;
using Rooksh.Uci;

namespace Rooksh.Cli;

/// <summary>Shared translation from configuration to match/engine-layer types.</summary>
public static class MatchSetup
{
    public static TimeControl ToTimeControl(MatchConfig match) =>
        match.TimeControl.ToLowerInvariant() switch
        {
            "fixed" => TimeControl.Fixed(TimeSpan.FromMinutes(match.BaseMinutes)),
            "sudden_death" or "sudden-death" => TimeControl.SuddenDeath(TimeSpan.FromMinutes(match.BaseMinutes)),
            "increment" or "fischer" => TimeControl.WithIncrement(
                TimeSpan.FromMinutes(match.BaseMinutes),
                TimeSpan.FromSeconds(match.IncrementSeconds)),
            _ => TimeControl.WithIncrement(
                TimeSpan.FromMinutes(match.BaseMinutes),
                TimeSpan.FromSeconds(match.IncrementSeconds)),
        };

    public static MatchMode ToMatchMode(string mode) =>
        mode.ToLowerInvariant() switch
        {
            "engine_vs_engine" or "engine-vs-engine" or "evs" => MatchMode.EngineVsEngine,
            "human_vs_engine" or "human-vs-engine" => MatchMode.HumanVsEngine,
            "human_vs_human" or "human-vs-human" => MatchMode.HumanVsHuman,
            "analysis" => MatchMode.Analysis,
            _ => MatchMode.EngineVsEngine,
        };

    public static UciEngineOptions ToEngineOptions(EngineConfig engine, string slot, RookshConfig config)
    {
        if (string.IsNullOrWhiteSpace(engine.Path))
        {
            throw new InvalidOperationException(
                $"Engine '{slot}' has no path. Set [engine_{slot}] path in rooksh.toml, ROOKSH_ENGINE_{slot.ToUpperInvariant()}_PATH, or --engine-{slot}-path.");
        }

        return new UciEngineOptions
        {
            Path = engine.Path!,
            Args = engine.Args,
            HashMb = engine.HashMb,
            Threads = engine.Threads,
            MultiPv = engine.MultiPv,
            Ponder = config.Match.Ponder,
            LimitStrength = engine.LimitStrength,
            UciElo = engine.UciElo,
        };
    }
}
