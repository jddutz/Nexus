namespace Nexus.Graphics.Vulkan;

/// <summary>Tracks command and completed-frame performance metrics for Vulkan rendering.</summary>
public class PerformanceMetrics
{
    private TimeSpan _totalFrameTime;
    private double _minimumAverageFrameRateThreshold;
    private TimeSpan _maximumFrameTimeThreshold = TimeSpan.MaxValue;

    /// <summary>Gets the time at which metric collection began.</summary>
    public DateTime StartTime { get; } = DateTime.Now;

    /// <summary>Gets the total number of successfully recorded Vulkan commands.</summary>
    public long Commands { get; private set; }

    /// <summary>Gets the number of frames that were submitted successfully.</summary>
    public long Frames { get; private set; }

    /// <summary>Gets the sum of completed frame durations.</summary>
    public TimeSpan TotalFrameTime => _totalFrameTime;

    /// <summary>Gets the average frame rate across completed frames.</summary>
    public double AverageFrameRate =>
        _totalFrameTime <= TimeSpan.Zero ? 0 : Frames / _totalFrameTime.TotalSeconds;

    /// <summary>Gets the longest completed frame duration.</summary>
    public TimeSpan LongestFrameTime { get; private set; }

    /// <summary>Gets the current number of buffers tracked by <see cref="BufferManager"/>.</summary>
    public int BuffersStored { get; private set; }

    /// <summary>Gets the highest number of buffers tracked at one time.</summary>
    public int MaximumBuffersStored { get; private set; }

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
        Commands++;
    }

    /// <summary>Records the duration of a successfully submitted frame.</summary>
    /// <param name="frameTime">The elapsed duration of the frame.</param>
    /// <exception cref="ArgumentOutOfRangeException">The duration is negative.</exception>
    public void RecordFrame(TimeSpan frameTime)
    {
        if (frameTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(frameTime));

        Frames++;
        _totalFrameTime += frameTime;
        if (frameTime > LongestFrameTime)
            LongestFrameTime = frameTime;
    }

    /// <summary>Updates the live and peak counts for buffers managed by Vulkan.</summary>
    /// <param name="count">The number of currently stored buffers.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    internal void RecordBuffersStored(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        BuffersStored = count;
        MaximumBuffersStored = Math.Max(MaximumBuffersStored, count);
    }

    /// <summary>Writes the current frame-rate, longest-frame, and command totals to the debug output.</summary>
    public void Output()
    {
        Debug.WriteLine(
            $"Vulkan Performance Metrics: AverageFrameRate={AverageFrameRate:F2} FPS, "
                + $"LongestFrameTime={LongestFrameTime.TotalMilliseconds:F2} ms, "
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
