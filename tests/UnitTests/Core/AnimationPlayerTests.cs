using Nexus.Core;

namespace Tests;

public class AnimationPlayerTests
{
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
