using Nexus.Core;

namespace Tests;

public class AnimationPlayerTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void EightAsepriteFramesTakeEightHundredMillisecondsAtAnyUpdateRate(int updatesPerSecond)
    {
        var value = -1;
        var player = new AnimationPlayer<int>(frame => value = frame);
        player.Play(new(Enumerable.Range(0, 8).Select(frame =>
            new AnimationFrame<int>(frame, TimeSpan.FromMilliseconds(100)))));
        long elapsed = 0;
        var step = TimeSpan.TicksPerSecond / updatesPerSecond;
        var almostFinished = TimeSpan.FromMilliseconds(799).Ticks;
        while (elapsed < almostFinished)
        {
            var ticks = Math.Min(step, almostFinished - elapsed);
            player.Advance(TimeSpan.FromTicks(ticks));
            elapsed += ticks;
            Assert.Equal((int)(elapsed / TimeSpan.FromMilliseconds(100).Ticks), value);
            Assert.False(player.IsCompleted);
        }
        player.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(player.IsCompleted);
    }

    [Fact]
    public void HoldsEachValueForItsDurationAndCompletesOnce()
    {
        var values = new List<int>();
        var player = new AnimationPlayer<int>(values.Add);
        var completed = 0;
        player.Completed += () => completed++;
        player.Play(new([new(1, TimeSpan.FromMilliseconds(80)), new(2, TimeSpan.FromMilliseconds(80))]));
        player.Advance(TimeSpan.FromMilliseconds(79));
        Assert.Equal([1], values);
        player.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal([1, 2], values);
        player.Advance(TimeSpan.FromMilliseconds(79));
        Assert.False(player.IsCompleted);
        player.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(player.IsCompleted);
        player.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, completed);
    }

    [Fact]
    public void CarriesTimeAcrossQueuedAnimationsAndRetainsFinalValue()
    {
        var value = 0;
        var player = new AnimationPlayer<int>(v => value = v);
        player.Enqueue(new([new(1, TimeSpan.FromMilliseconds(80))]));
        player.Enqueue(new([new(2, TimeSpan.FromMilliseconds(250))]));
        player.Advance(TimeSpan.FromMilliseconds(329));
        Assert.Equal(2, value);
        Assert.False(player.IsCompleted);
        player.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(player.IsCompleted);
        Assert.Equal(2, value);
    }

    [Fact]
    public void SupportsLargeLoopStepsReplacementStopAndValidation()
    {
        var value = 0;
        var player = new AnimationPlayer<int>(v => value = v);
        var loop = new Animation<int>([new(1, TimeSpan.FromTicks(10)), new(2, TimeSpan.FromTicks(20))], true);
        player.Play(loop);
        player.Advance(TimeSpan.MaxValue);
        Assert.Equal(1, value);
        player.Advance(TimeSpan.FromTicks(3));
        Assert.Equal(2, value);
        player.Play(new([new(3, TimeSpan.FromTicks(1))]));
        Assert.Equal(3, value);
        player.Stop();
        Assert.True(player.IsCompleted);
        Assert.Throws<ArgumentOutOfRangeException>(() => player.Advance(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentException>(() => new Animation<int>([]));
        Assert.Throws<ArgumentException>(() => new Animation<int>([new(1, TimeSpan.Zero)]));
    }
}
