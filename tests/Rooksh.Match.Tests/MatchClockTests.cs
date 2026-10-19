using Microsoft.Extensions.Time.Testing;
using Rooksh.Chess;

namespace Rooksh.Match.Tests;

public class MatchClockTests
{
    [Fact]
    public void Increment_control_charges_elapsed_and_adds_bonus()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new MatchClock(TimeControl.WithIncrement(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)), time);

        clock.Remaining(ChessSide.White).ShouldBe(TimeSpan.FromSeconds(10));

        clock.StartTurn(ChessSide.White);
        time.Advance(TimeSpan.FromSeconds(3));
        clock.CompleteTurn(ChessSide.White);

        // 10s - 3s + 2s = 9s
        clock.Remaining(ChessSide.White).ShouldBe(TimeSpan.FromSeconds(9));
        clock.Remaining(ChessSide.Black).ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Sudden_death_has_no_increment()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new MatchClock(TimeControl.SuddenDeath(TimeSpan.FromSeconds(30)), time);

        clock.StartTurn(ChessSide.Black);
        time.Advance(TimeSpan.FromSeconds(5));
        clock.CompleteTurn(ChessSide.Black);

        clock.Remaining(ChessSide.Black).ShouldBe(TimeSpan.FromSeconds(25));
    }

    [Fact]
    public void Fixed_control_resets_each_move()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new MatchClock(TimeControl.Fixed(TimeSpan.FromSeconds(5)), time);

        clock.StartTurn(ChessSide.White);
        time.Advance(TimeSpan.FromSeconds(2));
        clock.CompleteTurn(ChessSide.White);

        clock.Remaining(ChessSide.White).ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Running_clock_reduces_remaining_in_real_time()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new MatchClock(TimeControl.SuddenDeath(TimeSpan.FromSeconds(10)), time);

        clock.StartTurn(ChessSide.White);
        time.Advance(TimeSpan.FromSeconds(4));

        clock.Remaining(ChessSide.White).ShouldBe(TimeSpan.FromSeconds(6));
        clock.HasFlagged(ChessSide.White).ShouldBeFalse();
    }

    [Fact]
    public void Flags_when_budget_is_exhausted()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new MatchClock(TimeControl.SuddenDeath(TimeSpan.FromSeconds(10)), time);

        clock.StartTurn(ChessSide.White);
        time.Advance(TimeSpan.FromSeconds(10));

        clock.HasFlagged(ChessSide.White).ShouldBeTrue();
        clock.Remaining(ChessSide.White).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Millisecond_conversion_is_clamped()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var clock = new MatchClock(TimeControl.SuddenDeath(TimeSpan.FromMilliseconds(1500)), time);

        clock.RemainingMilliseconds(ChessSide.White).ShouldBe(1500);
    }
}
