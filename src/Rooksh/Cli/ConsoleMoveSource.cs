using Rooksh.Chess;
using Rooksh.Match;

namespace Rooksh.Cli;

/// <summary>
/// Line-based human move source. Keeps the human/engine seam identical to the engine source,
/// so the TUI can replace it without touching the orchestrator.
/// </summary>
public sealed class ConsoleMoveSource : IMoveSource
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly bool _showLegalMoves;

    public ConsoleMoveSource(string displayName, TextReader? input = null, TextWriter? output = null, bool showLegalMoves = true)
    {
        DisplayName = displayName;
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
        _showLegalMoves = showLegalMoves;
    }

    public string DisplayName { get; }

    public Task<MoveChoice> ChooseMoveAsync(MoveRequest request, CancellationToken cancellationToken)
    {
        if (_showLegalMoves)
        {
            _output.WriteLine($"Legal: {string.Join(' ', request.LegalMoves.Select(m => m.San))}");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            _output.Write($"{DisplayName} ({request.SideToMove}) > ");
            var line = _input.ReadLine();
            if (line is null)
            {
                return Task.FromResult(MoveChoice.Forfeit("input closed"));
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line is "resign" or "quit" or "exit")
            {
                return Task.FromResult(MoveChoice.Forfeit("resigned"));
            }

            var normalized = ChessMove.NormalizeUci(line);
            var match = request.LegalMoves.FirstOrDefault(m => string.Equals(m.Uci, normalized, StringComparison.OrdinalIgnoreCase))
                ?? request.LegalMoves.FirstOrDefault(m => string.Equals(m.San, line, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return Task.FromResult(MoveChoice.Of(match.Uci));
            }

            _output.WriteLine($"Illegal move '{line}'.");
        }

        return Task.FromResult(MoveChoice.Forfeit("cancelled"));
    }
}
