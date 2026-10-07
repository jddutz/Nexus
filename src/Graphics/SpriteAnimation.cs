namespace Nexus.Graphics;

/// <summary>A normalized atlas region displayed for a positive duration.</summary>
public record SpriteFrame(Vector4D<float> TexCoord, TimeSpan Duration);

/// <summary>An immutable sequence of atlas frames with shared size and anchor.</summary>
public sealed class SpriteAnimation
{
    /// <summary>Creates an animation from a nonempty sequence of positive-duration frames.</summary>
    public SpriteAnimation(IEnumerable<SpriteFrame> frames, bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var copy = frames.ToArray();
        if (copy.Length == 0 || copy.Any(frame => frame is null || frame.Duration <= TimeSpan.Zero))
            throw new ArgumentException("Animation frames must have positive durations.", nameof(frames));
        Frames = Array.AsReadOnly(copy);
        Loop = loop;
        Duration = TimeSpan.FromTicks(copy.Aggregate(0L, (ticks, frame) => checked(ticks + frame.Duration.Ticks)));
    }

    /// <summary>Gets the ordered frames.</summary>
    public IReadOnlyList<SpriteFrame> Frames { get; }
    /// <summary>Gets whether playback repeats.</summary>
    public bool Loop { get; }
    /// <summary>Gets the duration of one cycle.</summary>
    public TimeSpan Duration { get; }
}

/// <summary>Advances atlas frames independently of sprite rendering.</summary>
public sealed class SpriteAnimationPlayer
{
    private long _elapsedTicks;
    private readonly SpriteInstance _instance;
    private readonly SpriteAnimation _animation;

    /// <summary>Starts playback and assigns the first frame.</summary>
    public SpriteAnimationPlayer(SpriteInstance instance, SpriteAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(animation);
        _instance = instance;
        _animation = animation;
        Restart();
    }

    /// <summary>Gets whether nonlooping playback reached its final frame.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Restarts playback at the first frame.</summary>
    public void Restart()
    {
        _elapsedTicks = 0;
        IsCompleted = false;
        _instance.TexCoord = _animation.Frames[0].TexCoord;
    }

    /// <summary>Advances playback, preserving the final frame after nonlooping completion.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (IsCompleted) return;
        var duration = _animation.Duration.Ticks;
        if (_animation.Loop)
        {
            var step = elapsed.Ticks % duration;
            _elapsedTicks = _elapsedTicks >= duration - step
                ? _elapsedTicks - (duration - step) : _elapsedTicks + step;
        }
        else
        {
            if (elapsed.Ticks >= duration - _elapsedTicks)
            {
                _elapsedTicks = duration;
                IsCompleted = true;
                _instance.TexCoord = _animation.Frames[^1].TexCoord;
                return;
            }
            _elapsedTicks += elapsed.Ticks;
        }
        var remaining = _elapsedTicks;
        foreach (var frame in _animation.Frames)
        {
            if (remaining < frame.Duration.Ticks)
            {
                _instance.TexCoord = frame.TexCoord;
                return;
            }
            remaining -= frame.Duration.Ticks;
        }
    }
}
