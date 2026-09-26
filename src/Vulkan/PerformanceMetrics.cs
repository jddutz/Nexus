namespace Nexus.Graphics.Vulkan;

/// <summary>Tracks command and completed-frame performance metrics for Vulkan rendering.</summary>
public class PerformanceMetrics
{
    private readonly bool _enabled;

    /// <summary>Creates a metrics collector configured by the Vulkan options.</summary>
    /// <param name="options">The Vulkan settings; a directly constructed collector is enabled by default.</param>
    public PerformanceMetrics(IOptions<VulkanSettings>? options = null)
    {
        _enabled = options?.Value.EnablePerformanceMetrics ?? true;
    }

    /// <summary>Gets whether this collector is enabled.</summary>
    public bool IsEnabled => _enabled;
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private TimeSpan _totalFrameTime;
    private TimeSpan? _timeToFirstFrame;
    private TimeSpan? _firstFrameDuration;
    private double _minimumAverageFrameRateThreshold;
    private TimeSpan _maximumFrameTimeThreshold = TimeSpan.MaxValue;
    private int _buffersStored;
    private int _maximumBuffersStored;

    /// <summary>Gets the time at which metric collection began.</summary>
    public DateTime StartTime { get; } = DateTime.Now;

    /// <summary>Gets the elapsed time since metric collection began.</summary>
    public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(_startTimestamp);

    /// <summary>Gets the total number of successfully recorded Vulkan commands.</summary>
    public long Commands { get; private set; }

    /// <summary>Gets the number of frames that were submitted successfully.</summary>
    public long Frames { get; private set; }

    /// <summary>Gets the sum of completed frame durations.</summary>
    public TimeSpan TotalFrameTime => _totalFrameTime;

    /// <summary>Gets elapsed time from metric collection start to the first successfully submitted frame.</summary>
    public TimeSpan? TimeToFirstFrame => _timeToFirstFrame;

    /// <summary>Gets the duration of the first successfully submitted frame.</summary>
    public TimeSpan? FirstFrameDuration => _firstFrameDuration;

    /// <summary>Gets the average frame rate across the total elapsed time.</summary>
    public double AverageFrameRate =>
        ElapsedTime <= TimeSpan.Zero ? 0 : Frames / ElapsedTime.TotalSeconds;

    /// <summary>Gets the longest completed frame duration.</summary>
    public TimeSpan LongestFrameTime { get; private set; }

    /// <summary>Gets the current number of live Vulkan buffers tracked by their owners.</summary>
    public int BuffersStored => Volatile.Read(ref _buffersStored);

    /// <summary>Gets the highest number of live Vulkan buffers tracked at one time.</summary>
    public int MaximumBuffersStored => Volatile.Read(ref _maximumBuffersStored);

    /// <summary>Gets or sets the minimum average frame rate that triggers a warning.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The threshold is negative or NaN.</exception>
    public double MinimumAverageFrameRateThreshold
    {
        get => _minimumAverageFrameRateThreshold;
        set
        {
            if (value < 0 || double.IsNaN(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            _minimumAverageFrameRateThreshold = value;
        }
    }

    /// <summary>Gets or sets the longest frame duration that does not trigger a warning.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The threshold is negative.</exception>
    public TimeSpan MaximumFrameTimeThreshold
    {
        get => _maximumFrameTimeThreshold;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));

            _maximumFrameTimeThreshold = value;
        }
    }

    /// <summary>Records a Vulkan command after its recording call succeeds.</summary>
    /// <param name="cmd">The command that was recorded.</param>
    public void Record(IVulkanCommand cmd)
    {
        if (!_enabled)
            return;

        ArgumentNullException.ThrowIfNull(cmd);
        Commands++;
    }

    /// <summary>Records the duration of a successfully submitted frame.</summary>
    /// <param name="frameTime">The elapsed duration of the frame.</param>
    /// <exception cref="ArgumentOutOfRangeException">The duration is negative.</exception>
    public void RecordFrame(TimeSpan frameTime)
    {
        if (!_enabled)
            return;

        if (frameTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(frameTime));

        if (Frames == 0)
        {
            _timeToFirstFrame = ElapsedTime;
            _firstFrameDuration = frameTime;
        }

        Frames++;
        _totalFrameTime += frameTime;
        if (frameTime > LongestFrameTime)
            LongestFrameTime = frameTime;
    }

    /// <summary>Records a newly owned Vulkan buffer.</summary>
    internal void RecordBufferCreated()
    {
        if (!_enabled)
            return;

        var count = Interlocked.Increment(ref _buffersStored);
        var maximum = Volatile.Read(ref _maximumBuffersStored);

        while (count > maximum)
        {
            var observed = Interlocked.CompareExchange(ref _maximumBuffersStored, count, maximum);
            if (observed == maximum)
                break;

            maximum = observed;
        }
    }

    /// <summary>Records destruction of a previously owned Vulkan buffer.</summary>
    internal void RecordBufferDestroyed()
    {
        if (!_enabled)
            return;

        Interlocked.Decrement(ref _buffersStored);
    }

    /// <summary>Writes the current frame-rate, longest-frame, and command totals to the debug output.</summary>
    public void Output()
    {
        if (!_enabled)
            return;

        var timeToFirstFrame = TimeToFirstFrame?.TotalSeconds.ToString("F2") ?? "n/a";
        var firstFrameDuration = FirstFrameDuration?.TotalMilliseconds.ToString("F2") ?? "n/a";

        Debug.WriteLine(
            $"Vulkan Performance Metrics: AverageFrameRate={AverageFrameRate:F2} FPS, "
                + $"LongestFrameTime={LongestFrameTime.TotalMilliseconds:F2} ms, "
                + $"ElapsedTime={ElapsedTime.TotalSeconds:F2} s, "
                + $"TimeToFirstFrame={timeToFirstFrame} s, "
                + $"FirstFrameDuration={firstFrameDuration} ms, "
                + $"Frames={Frames}, Commands={Commands}, "
                + $"BuffersStored={BuffersStored}, MaximumBuffersStored={MaximumBuffersStored}"
        );

        if (Frames > 0 && AverageFrameRate < MinimumAverageFrameRateThreshold)
            Debug.WriteLine(
                $"[WARN] Average frame rate {AverageFrameRate:F2} FPS is below "
                    + $"{MinimumAverageFrameRateThreshold:F2} FPS."
            );

        if (LongestFrameTime > MaximumFrameTimeThreshold)
            Debug.WriteLine(
                $"[WARN] Longest frame time {LongestFrameTime.TotalMilliseconds:F2} ms exceeds "
                    + $"{MaximumFrameTimeThreshold.TotalMilliseconds:F2} ms."
            );
    }
}
