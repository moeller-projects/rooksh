using Rooksh.Rendering;

namespace Rooksh.Rendering.Tests;

public class AnsiRendererTests
{
    [Fact]
    public void Initialize_switches_to_the_alt_screen_and_hides_the_cursor()
    {
        var writer = new StringWriter();
        using var renderer = new AnsiRenderer(writer, mouse: false);

        renderer.Initialize();

        var output = writer.ToString();
        output.ShouldContain("\u001b[?1049h");
        output.ShouldContain("\u001b[?25l");
        output.ShouldContain("\u001b[2J");
    }

    [Fact]
    public void Render_writes_changed_cells_with_cursor_moves()
    {
        var writer = new StringWriter();
        using var renderer = new AnsiRenderer(writer, trueColor: false, mouse: false);

        var buffer = new ScreenBuffer(3, 2);
        buffer.Set(1, 1, new Cell('X', TermColor.Indexed(9), TermColor.Default));

        renderer.Render(buffer);

        var output = writer.ToString();
        output.ShouldContain("\u001b[2;2H");
        output.ShouldContain("X");
        output.ShouldContain("\u001b[38;5;9m");
    }

    [Fact]
    public void Second_identical_render_writes_nothing()
    {
        var writer = new StringWriter();
        using var renderer = new AnsiRenderer(writer, mouse: false);

        var buffer = new ScreenBuffer(3, 2);
        buffer.Set(0, 0, new Cell('A'));

        renderer.Render(buffer);
        var afterFirst = writer.ToString().Length;

        renderer.Render(buffer);

        writer.ToString().Length.ShouldBe(afterFirst);
        renderer.FramesRendered.ShouldBe(2);
    }

    [Fact]
    public void Only_the_changed_cell_is_emitted_on_the_next_frame()
    {
        var writer = new StringWriter();
        using var renderer = new AnsiRenderer(writer, mouse: false);

        var buffer = new ScreenBuffer(4, 1);
        buffer.Set(0, 0, new Cell('A'));
        buffer.Set(1, 0, new Cell('B'));
        renderer.Render(buffer);

        var afterFirst = writer.ToString().Length;

        buffer.Set(1, 0, new Cell('C'));
        renderer.Render(buffer);

        var appended = writer.ToString()[afterFirst..];
        appended.ShouldContain("C");
        appended.ShouldNotContain("A");
    }

    [Fact]
    public void Truecolor_rgb_is_emitted_when_enabled()
    {
        var writer = new StringWriter();
        using var renderer = new AnsiRenderer(writer, trueColor: true, mouse: false);

        var buffer = new ScreenBuffer(1, 1);
        buffer.Set(0, 0, new Cell('X', TermColor.Rgb(1, 2, 3), TermColor.Rgb(4, 5, 6)));

        renderer.Render(buffer);

        var output = writer.ToString();
        output.ShouldContain("38;2;1;2;3");
        output.ShouldContain("48;2;4;5;6");
    }

    [Fact]
    public void Truecolor_falls_back_to_256_when_disabled()
    {
        var writer = new StringWriter();
        using var renderer = new AnsiRenderer(writer, trueColor: false, mouse: false);

        var buffer = new ScreenBuffer(1, 1);
        buffer.Set(0, 0, new Cell('X', TermColor.Rgb(255, 0, 0), TermColor.Default));

        renderer.Render(buffer);

        writer.ToString().ShouldContain("38;5;196");
    }

    [Fact]
    public void Shutdown_restores_the_terminal()
    {
        var writer = new StringWriter();
        var renderer = new AnsiRenderer(writer, mouse: false);

        renderer.Initialize();
        renderer.Shutdown();

        var output = writer.ToString();
        output.ShouldContain("\u001b[?25h");
        output.ShouldContain("\u001b[?1049l");
    }

    [Fact]
    public void Resize_forces_a_clear_and_full_redraw()
    {
        var writer = new StringWriter();
        using var renderer = new AnsiRenderer(writer, mouse: false);

        renderer.Render(new ScreenBuffer(2, 2));
        var afterFirst = writer.ToString().Length;

        renderer.Render(new ScreenBuffer(4, 4));

        writer.ToString()[afterFirst..].ShouldContain("\u001b[2J");
    }
}
