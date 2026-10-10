namespace Nexus.Core;

/// <summary>A value held for a positive duration.</summary>
public sealed record AnimationFrame<T>(T Value, TimeSpan Duration);

/// <summary>An immutable sequence of timed values.</summary>
public sealed class Animation<T>
{
    public Animation(IEnumerable<AnimationFrame<T>> frames, bool loop = false)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var copy = frames.ToArray();
        if (copy.Length == 0 || copy.Any(frame => frame is null || frame.Duration <= TimeSpan.Zero))
            throw new ArgumentException("Animation frames must have positive durations.", nameof(frames));
        Frames = Array.AsReadOnly(copy);
        Duration = TimeSpan.FromTicks(copy.Aggregate(0L, (sum, frame) => checked(sum + frame.Duration.Ticks)));
        Loop = loop;
    }
    public IReadOnlyList<AnimationFrame<T>> Frames { get; }
    public TimeSpan Duration { get; }
    public bool Loop { get; }
}

/// <summary>Plays and queues timed values through any property setter. Advance once per update.</summary>
public sealed class AnimationPlayer<T>(Action<T> apply)
{
    private readonly Action<T> _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    private readonly Queue<Animation<T>> _queue = new();
    private Animation<T>? _animation;
    private long _elapsedTicks;
    private int _frame = -1;
    public bool IsCompleted => _animation is null;
    /// <summary>Raised once when playback and all queued animations finish.</summary>
    public event Action? Completed;

    public void Play(Animation<T> animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        _queue.Clear();
        Start(animation);
    }
    public void Enqueue(Animation<T> animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        if (_animation is null) Start(animation);
        else _queue.Enqueue(animation);
    }
    public void Stop()
    {
        _queue.Clear();
        _animation = null;
        _frame = -1;
    }
    private void Start(Animation<T> animation)
    {
        _animation = animation;
        _elapsedTicks = 0;
        _frame = 0;
        _apply(animation.Frames[0].Value);
    }
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        var ticks = elapsed.Ticks;
        while (_animation is { } animation)
        {
            var duration = animation.Duration.Ticks;
            if (animation.Loop)
            {
                var step = ticks % duration;
                _elapsedTicks = _elapsedTicks >= duration - step
                    ? _elapsedTicks - (duration - step) : _elapsedTicks + step;
            }
            else if (ticks >= duration - _elapsedTicks)
            {
                ticks -= duration - _elapsedTicks;
                if (_frame != animation.Frames.Count - 1)
                    _apply(animation.Frames[^1].Value);
                if (_queue.TryDequeue(out var next)) { Start(next); continue; }
                Stop();
                Completed?.Invoke();
                return;
            }
            else _elapsedTicks += ticks;
            var remaining = _elapsedTicks;
            for (var index = 0; index < animation.Frames.Count; index++)
            {
                var frame = animation.Frames[index];
                if (remaining < frame.Duration.Ticks)
                {
                    if (_frame != index) { _frame = index; _apply(frame.Value); }
                    return;
                }
                remaining -= frame.Duration.Ticks;
            }
        }
    }
}
