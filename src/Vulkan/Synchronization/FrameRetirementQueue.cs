namespace Nexus.Graphics.Vulkan.Synchronization;

/// <summary>Defers resource release until every submitted frame that may reference it completes.</summary>
internal sealed class FrameRetirementQueue<T>
    where T : notnull
{
    private readonly object _gate = new();
    private readonly uint _maxFramesInFlight;
    private readonly Action<T> _release;
    private readonly HashSet<uint> _inFlightFrames = [];
    private readonly Dictionary<T, HashSet<uint>> _retiredResources = [];

    /// <summary>Creates a retirement queue for the configured frame-slot count.</summary>
    /// <param name="maxFramesInFlight">The number of frame slots managed by synchronization.</param>
    /// <param name="release">The action that releases a resource after its final use.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxFramesInFlight"/> is zero.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="release"/> is null.</exception>
    public FrameRetirementQueue(uint maxFramesInFlight, Action<T> release)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maxFramesInFlight);
        _maxFramesInFlight = maxFramesInFlight;
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    /// <summary>Records a frame slot whose submitted commands may reference live resources.</summary>
    /// <param name="frameIndex">The submitted slot index.</param>
    /// <exception cref="ArgumentOutOfRangeException">The frame index is outside the configured range.</exception>
    /// <exception cref="InvalidOperationException">The slot was submitted before its prior use completed.</exception>
    public void OnFrameSubmitted(uint frameIndex)
    {
        ValidateFrameIndex(frameIndex);
        lock (_gate)
        {
            if (!_inFlightFrames.Add(frameIndex))
                throw new InvalidOperationException(
                    $"Frame slot {frameIndex} was submitted before its previous submission completed."
                );
        }
    }

    /// <summary>Marks a submitted slot complete and releases resources no longer used by any slot.</summary>
    /// <param name="frameIndex">The completed slot index.</param>
    /// <exception cref="ArgumentOutOfRangeException">The frame index is outside the configured range.</exception>
    public void OnFrameCompleted(uint frameIndex)
    {
        ValidateFrameIndex(frameIndex);
        List<T> readyToRelease = [];
        lock (_gate)
        {
            _inFlightFrames.Remove(frameIndex);

            foreach (var (resource, waitingFrames) in _retiredResources.ToArray())
            {
                waitingFrames.Remove(frameIndex);
                if (waitingFrames.Count != 0)
                    continue;

                _retiredResources.Remove(resource);
                readyToRelease.Add(resource);
            }
        }

        foreach (var resource in readyToRelease)
            _release(resource);
    }

    /// <summary>Defers release until all currently submitted frames complete.</summary>
    /// <param name="resource">The resource no longer used by future submissions.</param>
    /// <exception cref="InvalidOperationException">The resource is already pending retirement.</exception>
    public void Retire(T resource)
    {
        var releaseImmediately = false;
        lock (_gate)
        {
            if (_retiredResources.ContainsKey(resource))
                throw new InvalidOperationException("The resource is already pending retirement.");

            if (_inFlightFrames.Count == 0)
            {
                releaseImmediately = true;
            }
            else
            {
                _retiredResources.Add(resource, new HashSet<uint>(_inFlightFrames));
            }
        }

        if (releaseImmediately)
            _release(resource);
    }

    /// <summary>Forgets tracked frames and pending resources after the owning device is idle.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _inFlightFrames.Clear();
            _retiredResources.Clear();
        }
    }

    /// <summary>Rejects a frame index outside this queue's configured frame slots.</summary>
    /// <param name="frameIndex">The frame slot to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the configured range.</exception>
    private void ValidateFrameIndex(uint frameIndex)
    {
        if (frameIndex >= _maxFramesInFlight)
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                $"Frame index must be less than {_maxFramesInFlight}."
            );
    }
}
