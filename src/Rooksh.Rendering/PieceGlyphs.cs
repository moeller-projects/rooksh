namespace Rooksh.Rendering;

/// <summary>Maps FEN piece characters to display glyphs, with an ASCII fallback.</summary>
public static class PieceGlyphs
{
    public static char Glyph(char fenChar, bool ascii = false)
    {
        if (fenChar is ' ' or '\0' or '.')
        {
            return ' ';
        }

        if (ascii)
        {
            // Uppercase = white, lowercase = black.
            return fenChar;
        }

        var white = char.IsUpper(fenChar);
        return char.ToUpperInvariant(fenChar) switch
        {
            'K' => white ? '♔' : '♚',
            'Q' => white ? '♕' : '♛',
            'R' => white ? '♖' : '♜',
            'B' => white ? '♗' : '♝',
            'N' => white ? '♘' : '♞',
            'P' => white ? '♙' : '♟',
            _ => ' ',
        };
    }

    public static bool IsWhite(char fenChar) => char.IsUpper(fenChar);

    /// <summary>Parses the board field of a FEN into <c>[file, rank]</c> with rank 0 = rank 1.</summary>
    public static char[,] ParseBoard(string fen)
    {
        var board = new char[8, 8];
        for (var file = 0; file < 8; file++)
        {
            for (var rank = 0; rank < 8; rank++)
            {
                board[file, rank] = ' ';
            }
        }

        if (string.IsNullOrWhiteSpace(fen))
        {
            return board;
        }

        var boardField = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var ranks = boardField.Split('/');
        if (ranks.Length != 8)
        {
            return board;
        }

        for (var displayRow = 0; displayRow < 8; displayRow++)
        {
            var rank = 7 - displayRow;
            var file = 0;
            foreach (var c in ranks[displayRow])
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                    continue;
                }

                if (file < 8)
                {
                    board[file, rank] = c;
                }

                file++;
            }
        }

        return board;
    }

    public static bool WhiteToMove(string fen)
    {
        var fields = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length < 2 || fields[1] != "b";
    }
}
