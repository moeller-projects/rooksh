using Rooksh.Rendering;

namespace Rooksh.Cli;

/// <summary>
/// Best-effort terminal front-end. Keyboard input uses <see cref="Console.ReadKey(bool)"/> which is
/// portable; SGR mouse and richer sequences are decoded by <see cref="InputParser"/> and are wired
/// here when the platform exposes raw input.
/// </summary>
public sealed class ConsoleTerminal : IDisposable
{
    private bool _entered;

    public bool IsInteractive { get; } =
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public (int Width, int Height) Size
    {
        get
        {
            try
            {
                return (Math.Max(40, Console.WindowWidth), Math.Max(12, Console.WindowHeight));
            }
            catch
            {
                return (80, 24);
            }
        }
    }

    public void Enter()
    {
        if (_entered)
        {
            return;
        }

        _entered = true;
        try
        {
            Console.TreatControlCAsInput = true;
            Console.CursorVisible = false;
        }
        catch
        {
            // Non-interactive hosts may not support these.
        }
    }

    public void Exit()
    {
        if (!_entered)
        {
            return;
        }

        _entered = false;
        try
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            Console.TreatControlCAsInput = false;
        }
        catch
        {
            // ignore
        }
    }

    public bool KeyAvailable
    {
        get
        {
            try
            {
                return Console.KeyAvailable;
            }
            catch
            {
                return false;
            }
        }
    }

    public UiEvent Read() => Map(Console.ReadKey(intercept: true));

    public void Dispose() => Exit();

    internal static UiEvent Map(ConsoleKeyInfo info)
    {
        var key = info.Key switch
        {
            ConsoleKey.Enter => UiKey.Enter,
            ConsoleKey.Escape => UiKey.Escape,
            ConsoleKey.Tab => UiKey.Tab,
            ConsoleKey.Backspace => UiKey.Backspace,
            ConsoleKey.Delete => UiKey.Delete,
            ConsoleKey.Insert => UiKey.Insert,
            ConsoleKey.Home => UiKey.Home,
            ConsoleKey.End => UiKey.End,
            ConsoleKey.PageUp => UiKey.PageUp,
            ConsoleKey.PageDown => UiKey.PageDown,
            ConsoleKey.UpArrow => UiKey.Up,
            ConsoleKey.DownArrow => UiKey.Down,
            ConsoleKey.LeftArrow => UiKey.Left,
            ConsoleKey.RightArrow => UiKey.Right,
            ConsoleKey.Spacebar => UiKey.Space,
            ConsoleKey.F1 => UiKey.F1,
            ConsoleKey.F2 => UiKey.F2,
            ConsoleKey.F3 => UiKey.F3,
            ConsoleKey.F4 => UiKey.F4,
            ConsoleKey.F5 => UiKey.F5,
            ConsoleKey.F6 => UiKey.F6,
            ConsoleKey.F7 => UiKey.F7,
            ConsoleKey.F8 => UiKey.F8,
            ConsoleKey.F9 => UiKey.F9,
            ConsoleKey.F10 => UiKey.F10,
            ConsoleKey.F11 => UiKey.F11,
            ConsoleKey.F12 => UiKey.F12,
            _ => UiKey.None,
        };

        return new KeyEvent(
            key,
            info.KeyChar,
            (info.Modifiers & ConsoleModifiers.Control) != 0,
            (info.Modifiers & ConsoleModifiers.Alt) != 0,
            (info.Modifiers & ConsoleModifiers.Shift) != 0);
    }
}
