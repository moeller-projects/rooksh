namespace Rooksh.Rendering;

public enum UiKey
{
    None,
    Enter,
    Escape,
    Tab,
    Backspace,
    Delete,
    Insert,
    Home,
    End,
    PageUp,
    PageDown,
    Up,
    Down,
    Left,
    Right,
    Space,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
}

public enum MouseButton
{
    None,
    Left,
    Middle,
    Right,
    WheelUp,
    WheelDown,
}

public enum MouseAction
{
    Press,
    Release,
    Move,
}

/// <summary>Decoded input from the terminal.</summary>
public abstract record UiEvent;

public sealed record KeyEvent(
    UiKey Key,
    char Character = '\0',
    bool Control = false,
    bool Alt = false,
    bool Shift = false) : UiEvent
{
    public bool IsPrintable => Character >= ' ' && Character != '\u007f';
}

public sealed record MouseEvent(
    int X,
    int Y,
    MouseButton Button,
    MouseAction Action,
    bool Shift = false,
    bool Alt = false,
    bool Control = false) : UiEvent;
