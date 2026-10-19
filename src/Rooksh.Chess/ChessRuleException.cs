namespace Rooksh.Chess;

/// <summary>Raised when a move is rejected by the rules engine.</summary>
public sealed class ChessRuleException : Exception
{
    public ChessRuleException(string message) : base(message)
    {
    }

    public ChessRuleException(string message, Exception inner) : base(message, inner)
    {
    }
}
