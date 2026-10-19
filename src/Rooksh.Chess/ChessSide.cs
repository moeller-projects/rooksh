namespace Rooksh.Chess;

/// <summary>Side to move / piece ownership. Deliberately engine-agnostic.</summary>
public enum ChessSide
{
    White,
    Black,
}

public static class ChessSideExtensions
{
    public static ChessSide Opposite(this ChessSide side) =>
        side == ChessSide.White ? ChessSide.Black : ChessSide.White;

    public static string ToFenChar(this ChessSide side) =>
        side == ChessSide.White ? "w" : "b";

    public static string Label(this ChessSide side) =>
        side == ChessSide.White ? "White" : "Black";
}
