using Rooksh.Rendering;

namespace Rooksh.Rendering.Tests;

public class InputParserTests
{
    private static UiEvent Single(string input)
    {
        var parser = new InputParser();
        var events = parser.Feed(input).Concat(parser.Flush()).ToList();
        events.Count.ShouldBe(1);
        return events[0];
    }

    [Fact]
    public void Plain_printable_keys_produce_character_events()
    {
        var evt = Single("a").ShouldBeOfType<KeyEvent>();
        evt.Character.ShouldBe('a');
        evt.Key.ShouldBe(UiKey.None);
        evt.IsPrintable.ShouldBeTrue();
    }

    [Fact]
    public void Enter_tab_and_backspace_are_named_keys()
    {
        Single("\r").ShouldBeOfType<KeyEvent>().Key.ShouldBe(UiKey.Enter);
        Single("\n").ShouldBeOfType<KeyEvent>().Key.ShouldBe(UiKey.Enter);
        Single("\t").ShouldBeOfType<KeyEvent>().Key.ShouldBe(UiKey.Tab);
        Single("\u007f").ShouldBeOfType<KeyEvent>().Key.ShouldBe(UiKey.Backspace);
        Single(" ").ShouldBeOfType<KeyEvent>().Key.ShouldBe(UiKey.Space);
    }

    [Theory]
    [InlineData("\u001b[A", UiKey.Up)]
    [InlineData("\u001b[B", UiKey.Down)]
    [InlineData("\u001b[C", UiKey.Right)]
    [InlineData("\u001b[D", UiKey.Left)]
    [InlineData("\u001bOA", UiKey.Up)]
    [InlineData("\u001bOB", UiKey.Down)]
    public void Arrow_sequences_are_decoded(string input, UiKey expected)
    {
        Single(input).ShouldBeOfType<KeyEvent>().Key.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\u001b[1~", UiKey.Home)]
    [InlineData("\u001b[7~", UiKey.Home)]
    [InlineData("\u001b[4~", UiKey.End)]
    [InlineData("\u001b[8~", UiKey.End)]
    [InlineData("\u001b[3~", UiKey.Delete)]
    [InlineData("\u001b[2~", UiKey.Insert)]
    [InlineData("\u001b[5~", UiKey.PageUp)]
    [InlineData("\u001b[6~", UiKey.PageDown)]
    [InlineData("\u001b[H", UiKey.Home)]
    [InlineData("\u001b[F", UiKey.End)]
    public void Navigation_sequences_are_decoded(string input, UiKey expected)
    {
        Single(input).ShouldBeOfType<KeyEvent>().Key.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\u001bOP", UiKey.F1)]
    [InlineData("\u001bOQ", UiKey.F2)]
    [InlineData("\u001bOR", UiKey.F3)]
    [InlineData("\u001bOS", UiKey.F4)]
    [InlineData("\u001b[15~", UiKey.F5)]
    [InlineData("\u001b[17~", UiKey.F6)]
    [InlineData("\u001b[18~", UiKey.F7)]
    [InlineData("\u001b[19~", UiKey.F8)]
    [InlineData("\u001b[20~", UiKey.F9)]
    [InlineData("\u001b[21~", UiKey.F10)]
    [InlineData("\u001b[23~", UiKey.F11)]
    [InlineData("\u001b[24~", UiKey.F12)]
    public void Function_keys_are_decoded(string input, UiKey expected)
    {
        Single(input).ShouldBeOfType<KeyEvent>().Key.ShouldBe(expected);
    }

    [Fact]
    public void Shift_tab_is_reported()
    {
        var evt = Single("\u001b[Z").ShouldBeOfType<KeyEvent>();
        evt.Key.ShouldBe(UiKey.Tab);
        evt.Shift.ShouldBeTrue();
    }

    [Fact]
    public void Modified_arrows_report_ctrl_and_shift()
    {
        var ctrlUp = Single("\u001b[1;5A").ShouldBeOfType<KeyEvent>();
        ctrlUp.Key.ShouldBe(UiKey.Up);
        ctrlUp.Control.ShouldBeTrue();

        var shiftRight = Single("\u001b[1;2C").ShouldBeOfType<KeyEvent>();
        shiftRight.Key.ShouldBe(UiKey.Right);
        shiftRight.Shift.ShouldBeTrue();
    }

    [Fact]
    public void Alt_character_is_reported()
    {
        var evt = Single("\u001bx").ShouldBeOfType<KeyEvent>();
        evt.Character.ShouldBe('x');
        evt.Alt.ShouldBeTrue();
    }

    [Fact]
    public void Lone_escape_resolves_on_flush()
    {
        var parser = new InputParser();

        parser.Feed('\u001b').ShouldBeEmpty();
        var flushed = parser.Flush();

        flushed.Count.ShouldBe(1);
        flushed[0].ShouldBeOfType<KeyEvent>().Key.ShouldBe(UiKey.Escape);

        parser.Flush().ShouldBeEmpty();
    }

    [Fact]
    public void Mouse_press_and_release_are_decoded()
    {
        var press = Single("\u001b[<0;10;5M").ShouldBeOfType<MouseEvent>();
        press.Button.ShouldBe(MouseButton.Left);
        press.Action.ShouldBe(MouseAction.Press);
        press.X.ShouldBe(9);
        press.Y.ShouldBe(4);

        var release = Single("\u001b[<0;10;5m").ShouldBeOfType<MouseEvent>();
        release.Action.ShouldBe(MouseAction.Release);
    }

    [Fact]
    public void Mouse_wheel_and_modifiers_are_decoded()
    {
        var wheel = Single("\u001b[<64;3;3M").ShouldBeOfType<MouseEvent>();
        wheel.Button.ShouldBe(MouseButton.WheelUp);
        wheel.X.ShouldBe(2);
        wheel.Y.ShouldBe(2);

        var ctrlClick = Single("\u001b[<16;1;1M").ShouldBeOfType<MouseEvent>();
        ctrlClick.Button.ShouldBe(MouseButton.Left);
        ctrlClick.Control.ShouldBeTrue();
    }

    [Fact]
    public void Multiple_events_are_returned_in_order()
    {
        var parser = new InputParser();

        var events = parser.Feed("ab\u001b[A");

        events.Count.ShouldBe(3);
        events[0].ShouldBeOfType<KeyEvent>().Character.ShouldBe('a');
        events[1].ShouldBeOfType<KeyEvent>().Character.ShouldBe('b');
        events[2].ShouldBeOfType<KeyEvent>().Key.ShouldBe(UiKey.Up);
    }
}
