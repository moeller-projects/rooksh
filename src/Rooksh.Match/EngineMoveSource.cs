using Rooksh.Uci;

namespace Rooksh.Match;

/// <summary>Adapts an <see cref="EngineSession"/> to the orchestrator's <see cref="IMoveSource"/> seam.</summary>
public sealed class EngineMoveSource : IMoveSource, IInfoSource, IPonderSource
{
    private readonly EngineSession _session;
    private readonly bool _ponder;

    public EngineMoveSource(EngineSession session, bool ponder = false)
    {
        _session = session;
        _ponder = ponder;
        _session.InfoUpdated += info => InfoUpdated?.Invoke(info);
    }

    public string DisplayName => _session.DisplayName;

    public EngineSession Session => _session;

    public bool SupportsPonder => _session.SupportsPonder;

    public event Action<UciInfo>? InfoUpdated;

    public async Task<MoveChoice> ChooseMoveAsync(MoveRequest request, CancellationToken cancellationToken)
    {
        var best = await _session.ThinkAsync(request, _ponder, cancellationToken).ConfigureAwait(false);
        return ToChoice(best);
    }

    public Task<UciBestMove> ThinkAsync(MoveRequest request, CancellationToken cancellationToken) =>
        _session.ThinkAsync(request, ponder: false, cancellationToken);

    public void StartPonder(MoveRequest request) => _session.StartPonder(request);

    public Task<UciBestMove> PonderHitAsync(CancellationToken cancellationToken) =>
        _session.PonderHitAsync(cancellationToken);

    public Task<UciBestMove> StopPonderAsync(CancellationToken cancellationToken) =>
        _session.StopAsync(cancellationToken);

    internal static MoveChoice ToChoice(UciBestMove best) =>
        best.IsNone
            ? MoveChoice.Forfeit("engine reported no move")
            : MoveChoice.Of(best.Move!);
}
