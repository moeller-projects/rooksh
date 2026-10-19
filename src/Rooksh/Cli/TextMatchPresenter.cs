using Rooksh.Chess;
using Rooksh.Match;

namespace Rooksh.Cli;

/// <summary>Prints match events as plain text. Shared by <c>engine-match</c>, <c>play</c> and tests.</summary>
public sealed class TextMatchPresenter
{
    private readonly TextWriter _output;
    private readonly bool _showInfo;

    public TextMatchPresenter(TextWriter? output = null, bool showInfo = false)
    {
        _output = output ?? Console.Out;
        _showInfo = showInfo;
    }

    public void OnEvent(MatchEvent matchEvent)
    {
        switch (matchEvent)
        {
            case MatchStartedEvent started:
                _output.WriteLine($"A/White: {started.WhiteName}");
                _output.WriteLine($"B/Black: {started.BlackName}");
                _output.WriteLine($"Position: {(started.StartFen is { Length: > 0 } fen && fen != GeraChessGame.StandardStartFen ? fen : "startpos")}");
                _output.WriteLine($"TimeControl: {started.TimeControl}");
                _output.WriteLine();
                break;

            case MovePlayedEvent move:
                var number = ((move.Ply - 1) / 2) + 1;
                var prefix = move.Move.Side == ChessSide.White ? $"{number}." : $"{number}...";
                _output.WriteLine($"{prefix} {move.Move.San,-8} {FormatClock(move.WhiteRemaining, move.BlackRemaining)}");
                break;

            case EngineInfoEvent info when _showInfo:
                _output.WriteLine($"[{info.Side} {info.EngineName}] depth {info.Info.Depth} {info.Info.Score.ToDisplayString()}");
                break;

            case EngineFailedEvent failure:
                _output.WriteLine($"! {failure.Side} ({failure.EngineName}): {failure.Message}");
                break;

            case MatchEndedEvent ended:
                _output.WriteLine($"Result: {ended.Result} ({ended.Termination})");
                break;
        }
    }

    public static string FormatClock(TimeSpan white, TimeSpan black) =>
        $"[w {Format(white)} b {Format(black)}]";

    private static string Format(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
}
