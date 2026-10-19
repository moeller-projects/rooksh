using Rooksh.Uci;

namespace Rooksh.Match;

/// <summary>
/// Optional capability for a move source that can ponder: search the position expected after the
/// opponent's most likely reply, while the opponent is on the clock.
/// </summary>
public interface IPonderSource
{
    bool SupportsPonder { get; }

    Task<UciBestMove> ThinkAsync(MoveRequest request, CancellationToken cancellationToken);

    void StartPonder(MoveRequest request);

    Task<UciBestMove> PonderHitAsync(CancellationToken cancellationToken);

    Task<UciBestMove> StopPonderAsync(CancellationToken cancellationToken);
}
