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

/// <summary>Adapts generic timed-value playback to a sprite's atlas coordinates.</summary>
public sealed class SpriteAnimationPlayer
{
    private readonly AnimationPlayer<Vector4D<float>> _player;
    private readonly Animation<Vector4D<float>> _animation;
    public SpriteAnimationPlayer(SpriteInstance instance, SpriteAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(animation);
        _player = new(value => instance.TexCoord = value);
        _animation = new(animation.Frames.Select(frame => new AnimationFrame<Vector4D<float>>(frame.TexCoord, frame.Duration)), animation.Loop);
        Restart();
    }
    public bool IsCompleted => _player.IsCompleted;
    public void Restart() => _player.Play(_animation);
    public void Advance(TimeSpan elapsed) => _player.Advance(elapsed);
}
