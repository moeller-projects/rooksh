using AutoEndgameRules = global::Chess.AutoEndgameRules;
using ChessBoard = global::Chess.ChessBoard;

namespace Rooksh.Chess;

/// <summary>Default <see cref="IChessGameFactory"/> backed by Gera.Chess.</summary>
public sealed class GeraChessGameFactory : IChessGameFactory
{
    public static readonly GeraChessGameFactory Instance = new();

    public IChessGame Create() =>
        new GeraChessGame(new ChessBoard { AutoEndgameRules = AutoEndgameRules.All }, GeraChessGame.StandardStartFen);

    public IChessGame Create(string? fen) =>
        string.IsNullOrWhiteSpace(fen)
            ? Create()
            : new GeraChessGame(ChessBoard.LoadFromFen(fen.Trim(), AutoEndgameRules.All), fen);
}
