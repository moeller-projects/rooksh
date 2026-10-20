using System.Globalization;
using System.Text;

namespace Rooksh.Rendering;

/// <summary>
/// State machine that turns raw terminal characters into <see cref="UiEvent"/>s: printable keys,
/// control keys, arrow/navigation sequences (CSI and SS3), function keys, and SGR mouse reports.
/// </summary>
public sealed class InputParser
{
    private readonly StringBuilder _sequence = new();
    private State _state = State.Ground;

    private enum State
    {
        Ground,
        Escape,
        Csi,
        Ss3,
    }

    public IReadOnlyList<UiEvent> Feed(string text)
    {
        var events = new List<UiEvent>();
        foreach (var c in text)
        {
            events.AddRange(Feed(c));
        }

        return events;
    }

    public IReadOnlyList<UiEvent> Feed(char c)
    {
        var events = new List<UiEvent>();

        switch (_state)
        {
            case State.Ground:
                if (c == '\u001b')
                {
                    _state = State.Escape;
                }
                else
                {
                    events.Add(TranslatePlain(c));
                }

                break;

            case State.Escape:
                if (c == '[')
                {
                    _sequence.Clear();
                    _state = State.Csi;
                }
                else if (c == 'O')
                {
                    _sequence.Clear();
                    _state = State.Ss3;
                }
                else if (c == '\u001b')
                {
                    events.Add(new KeyEvent(UiKey.Escape));
                    _state = State.Escape;
                }
                else
                {
                    // Alt + key
                    var translated = TranslatePlain(c);
                    events.Add(translated switch
                    {
                        KeyEvent key => key with { Alt = true },
                        _ => translated,
                    });
                    _state = State.Ground;
                }

                break;

            case State.Csi:
                if (c >= '\u0040' && c <= '\u007e')
                {
                    var evt = CompleteCsi(_sequence.ToString(), c);
                    if (evt is not null)
                    {
                        events.Add(evt);
                    }

                    _state = State.Ground;
                }
                else
                {
                    _sequence.Append(c);
                }

                break;

            case State.Ss3:
                events.AddRange(CompleteSs3(c));
                _state = State.Ground;
                break;
        }

        return events;
    }

    /// <summary>Resolves a pending lone <c>ESC</c> at the end of a read.</summary>
    public IReadOnlyList<UiEvent> Flush()
    {
        if (_state == State.Escape)
        {
            _state = State.Ground;
            return [new KeyEvent(UiKey.Escape)];
        }

        _state = State.Ground;
        _sequence.Clear();
        return [];
    }

    private static UiEvent TranslatePlain(char c) => c switch
    {
        '\r' or '\n' => new KeyEvent(UiKey.Enter),
        '\t' => new KeyEvent(UiKey.Tab),
        '\b' or '\u007f' => new KeyEvent(UiKey.Backspace),
        ' ' => new KeyEvent(UiKey.Space, ' '),
        var control when control < ' ' => new KeyEvent(UiKey.None, control, Control: true),
        var printable => new KeyEvent(UiKey.None, printable),
    };

    private static UiEvent? CompleteCsi(string body, char final)
    {
        // SGR mouse: <b;x;yM or <b;x;ym
        if (body.StartsWith('<') && (final == 'M' || final == 'm'))
        {
            return ParseMouse(body[1..], final == 'M' ? MouseAction.Press : MouseAction.Release);
        }

        var (parameters, modifiers) = SplitModifiers(body);

        return final switch
        {
            'A' => new KeyEvent(UiKey.Up, Shift: modifiers.HasFlag(Mod.Shift), Alt: modifiers.HasFlag(Mod.Alt), Control: modifiers.HasFlag(Mod.Control)),
            'B' => new KeyEvent(UiKey.Down, Shift: modifiers.HasFlag(Mod.Shift), Alt: modifiers.HasFlag(Mod.Alt), Control: modifiers.HasFlag(Mod.Control)),
            'C' => new KeyEvent(UiKey.Right, Shift: modifiers.HasFlag(Mod.Shift), Alt: modifiers.HasFlag(Mod.Alt), Control: modifiers.HasFlag(Mod.Control)),
            'D' => new KeyEvent(UiKey.Left, Shift: modifiers.HasFlag(Mod.Shift), Alt: modifiers.HasFlag(Mod.Alt), Control: modifiers.HasFlag(Mod.Control)),
            'H' => new KeyEvent(UiKey.Home),
            'F' => new KeyEvent(UiKey.End),
            'Z' => new KeyEvent(UiKey.Tab, Shift: true),
            '~' => CompleteTilde(parameters),
            _ => null,
        };
    }

    private static UiEvent? CompleteTilde(string parameters)
    {
        var code = ParseFirstParameter(parameters);
        return code switch
        {
            1 or 7 => new KeyEvent(UiKey.Home),
            2 => new KeyEvent(UiKey.Insert),
            3 => new KeyEvent(UiKey.Delete),
            4 or 8 => new KeyEvent(UiKey.End),
            5 => new KeyEvent(UiKey.PageUp),
            6 => new KeyEvent(UiKey.PageDown),
            15 => new KeyEvent(UiKey.F5),
            17 => new KeyEvent(UiKey.F6),
            18 => new KeyEvent(UiKey.F7),
            19 => new KeyEvent(UiKey.F8),
            20 => new KeyEvent(UiKey.F9),
            21 => new KeyEvent(UiKey.F10),
            23 => new KeyEvent(UiKey.F11),
            24 => new KeyEvent(UiKey.F12),
            _ => null,
        };
    }

    private static IEnumerable<UiEvent> CompleteSs3(char c)
    {
        var key = c switch
        {
            'A' => UiKey.Up,
            'B' => UiKey.Down,
            'C' => UiKey.Right,
            'D' => UiKey.Left,
            'H' => UiKey.Home,
            'F' => UiKey.End,
            'P' => UiKey.F1,
            'Q' => UiKey.F2,
            'R' => UiKey.F3,
            'S' => UiKey.F4,
            _ => UiKey.None,
        };

        if (key == UiKey.None)
        {
            return [];
        }

        return [new KeyEvent(key)];
    }

    private static MouseEvent? ParseMouse(string body, MouseAction action)
    {
        var parts = body.Split(';');
        if (parts.Length != 3)
        {
            return null;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var buttonCode)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            return null;
        }

        var shift = (buttonCode & 4) != 0;
        var alt = (buttonCode & 8) != 0;
        var control = (buttonCode & 16) != 0;
        var wheel = (buttonCode & 64) != 0;

        MouseButton button;
        if (wheel)
        {
            button = (buttonCode & 1) == 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
        }
        else
        {
            button = (buttonCode & 3) switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Middle,
                2 => MouseButton.Right,
                _ => MouseButton.None,
            };
        }

        if (action == MouseAction.Press && !wheel && (buttonCode & 32) != 0)
        {
            action = MouseAction.Move;
        }

        return new MouseEvent(x - 1, y - 1, button, action, shift, alt, control);
    }

    [Flags]
    private enum Mod
    {
        None = 0,
        Shift = 1,
        Alt = 2,
        Control = 4,
    }

    private static (string Parameters, Mod Modifiers) SplitModifiers(string body)
    {
        var semicolon = body.LastIndexOf(';');
        if (semicolon < 0)
        {
            return (body, Mod.None);
        }

        var modifierText = body[(semicolon + 1)..];
        var parameters = body[..semicolon];
        if (!int.TryParse(modifierText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modifierValue))
        {
            return (body, Mod.None);
        }

        // xterm modifier encoding: 1 + (shift=1, alt=2, ctrl=4)
        var flags = modifierValue - 1;
        var modifiers = Mod.None;
        if ((flags & 1) != 0)
        {
            modifiers |= Mod.Shift;
        }

        if ((flags & 2) != 0)
        {
            modifiers |= Mod.Alt;
        }

        if ((flags & 4) != 0)
        {
            modifiers |= Mod.Control;
        }

        return (parameters, modifiers);
    }

    private static int ParseFirstParameter(string parameters)
    {
        var semicolon = parameters.IndexOf(';');
        var first = semicolon < 0 ? parameters : parameters[..semicolon];
        return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
    }
}
