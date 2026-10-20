namespace Rooksh.Rendering;

/// <summary>Maps between chess squares and screen cells.</summary>
public static class BoardGeometry
{
    public const int Files = 8;
    public const int Ranks = 8;
    public const int SquareWidth = 2;
    public const int SquareHeight = 1;

    public const int DisplayWidth = Files * SquareWidth;
    public const int DisplayHeight = Ranks * SquareHeight;

    /// <summary>Top-left cell of a square (file 0..7 = a..h, rank 0..7 = 1..8).</summary>
    public static (int X, int Y) SquareOrigin(int file, int rank, int originX, int originY, bool flipped)
    {
        var displayFile = flipped ? Files - 1 - file : file;
        var displayRank = flipped ? rank : Ranks - 1 - rank;
        return (originX + (displayFile * SquareWidth), originY + (displayRank * SquareHeight));
    }

    /// <summary>Square containing a screen cell, or null when outside the board.</summary>
    public static (int File, int Rank)? HitTest(int x, int y, int originX, int originY, bool flipped)
    {
        if (x < originX || y < originY)
        {
            return null;
        }

        var column = (x - originX) / SquareWidth;
        var row = (y - originY) / SquareHeight;
        if (column < 0 || column >= Files || row < 0 || row >= Ranks)
        {
            return null;
        }

        var file = flipped ? Files - 1 - column : column;
        var rank = flipped ? row : Ranks - 1 - row;
        return (file, rank);
    }

    public static string SquareName(int file, int rank) => $"{(char)('a' + file)}{rank + 1}";
}
