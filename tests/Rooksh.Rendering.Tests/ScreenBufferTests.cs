namespace Rooksh.Rendering.Tests;

public class ScreenBufferTests
{
    [Fact]
    public void Set_and_get_round_trip()
    {
        var buffer = new ScreenBuffer(4, 2);

        buffer.Set(1, 1, new Cell('X', TermColor.Indexed(1), TermColor.Indexed(2)));

        buffer[1, 1].Char.ShouldBe('X');
        buffer[1, 1].Foreground.Index.ShouldBe((byte)1);
        buffer[1, 1].Background.Index.ShouldBe((byte)2);
    }

    [Fact]
    public void Out_of_bounds_writes_are_ignored_and_reads_are_blank()
    {
        var buffer = new ScreenBuffer(2, 2);

        buffer.Set(-1, 0, new Cell('X'));
        buffer.Set(5, 5, new Cell('X'));

        buffer[5, 5].ShouldBe(Cell.Empty);
        buffer.ToText().ShouldBe("  \n  ");
    }

    [Fact]
    public void DrawText_clips_at_the_edge()
    {
        var buffer = new ScreenBuffer(3, 1);

        buffer.DrawText(1, 0, "hello", new Cell(' ', TermColor.Default, TermColor.Default));

        buffer.ToText().ShouldBe(" he");
    }

    [Fact]
    public void DrawBox_ascii_renders_a_frame()
    {
        var buffer = new ScreenBuffer(4, 3);

        buffer.DrawBox(0, 0, 4, 3, new Cell(' ', TermColor.Default, TermColor.Default), ascii: true);

        buffer.ToText().ShouldBe("+--+\n|  |\n+--+");
    }

    [Fact]
    public void DrawBox_unicode_uses_single_line_glyphs()
    {
        var buffer = new ScreenBuffer(4, 3);

        buffer.DrawBox(0, 0, 4, 3, new Cell(' ', TermColor.Default, TermColor.Default));

        buffer.ToText().ShouldBe("┌──┐\n│  │\n└──┘");
    }

    [Fact]
    public void Diff_reports_only_changed_cells()
    {
        var first = new ScreenBuffer(3, 2).ToArray();
        var second = new ScreenBuffer(3, 2);
        second.Set(1, 1, new Cell('x'));

        var diff = second.Diff(first).ToList();

        diff.Count.ShouldBe(1);
        diff[0].X.ShouldBe(1);
        diff[0].Y.ShouldBe(1);
        diff[0].Cell.Char.ShouldBe('x');
    }

    [Fact]
    public void Diff_against_null_reports_every_cell()
    {
        var buffer = new ScreenBuffer(2, 2);

        buffer.Diff(null).Count().ShouldBe(4);
    }

    [Fact]
    public void FillRect_and_lines_write_expected_cells()
    {
        var buffer = new ScreenBuffer(4, 3);
        buffer.FillRect(0, 0, 4, 1, new Cell('#'));
        buffer.HLine(0, 1, 4, '-', new Cell(' '));
        buffer.VLine(3, 1, 2, '|', new Cell(' '));

        buffer.ToText().ShouldBe("####\n---|\n   |");
    }
}
