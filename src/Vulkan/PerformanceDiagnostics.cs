namespace Nexus.Graphics.Vulkan;

/// <summary>Collects immutable Vulkan resource snapshots and one representative submitted command sequence.</summary>
public sealed class PerformanceDiagnostics
{
    private readonly bool _enabled;
    private readonly object _lock = new();
    private readonly Dictionary<
        (DrawableId DrawableId, string Category, string Label),
        PerformanceDiagnosticSnapshot
    > _snapshots = [];
    private readonly List<string> _pendingCommands = [];
    private ImmutableArray<string> _firstDrawnFrameCommands = [];
    private long _frameCount;
    private long _firstDrawnFrameCount;
    private bool _pendingFrameHasDraw;

    /// <summary>Creates a collector configured by application-wide diagnostics options.</summary>
    /// <param name="options">Core diagnostics settings; collection is disabled by default.</param>
    public PerformanceDiagnostics(IOptions<DiagnosticsSettings>? options = null)
    {
        _enabled = (options?.Value ?? new DiagnosticsSettings()).GraphicsInstrumentationEnabled;
    }

    /// <summary>Gets whether diagnostic collection is enabled.</summary>
    public bool IsEnabled => _enabled;

    /// <summary>Gets the number of successfully submitted frames observed while diagnostics are enabled.</summary>
    public long FrameCount
    {
        get
        {
            lock (_lock)
                return _frameCount;
        }
    }

    /// <summary>Gets the current frame number as a log suffix when diagnostics are enabled.</summary>
    internal string FrameCountLogSuffix
    {
        get
        {
            if (!_enabled)
                return string.Empty;

            lock (_lock)
                return $", FrameCount={_frameCount}";
        }
    }

    /// <summary>Gets the immutable drawable snapshots captured so far.</summary>
    public ImmutableArray<PerformanceDiagnosticSnapshot> Snapshots
    {
        get
        {
            lock (_lock)
                return [.. _snapshots.Values];
        }
    }

    /// <summary>Gets the command sequence from the first successfully submitted frame containing a draw.</summary>
    public ImmutableArray<string> FirstDrawnFrameCommandSequence
    {
        get
        {
            lock (_lock)
                return _firstDrawnFrameCommands;
        }
    }

    /// <summary>Stores a copied diagnostic snapshot, replacing the same drawable category and label.</summary>
    /// <param name="snapshot">The immutable evidence captured at resource creation or update.</param>
    public void Record(PerformanceDiagnosticSnapshot snapshot)
    {
        if (!_enabled)
            return;

        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_lock)
            _snapshots[(snapshot.DrawableId, snapshot.Category, snapshot.Label)] = snapshot;
    }

    /// <summary>Begins collecting commands for a frame that may contain the first draw.</summary>
    internal void BeginFrame()
    {
        if (!_enabled)
            return;

        lock (_lock)
        {
            if (!_firstDrawnFrameCommands.IsDefaultOrEmpty)
                return;

            _pendingCommands.Clear();
            _pendingFrameHasDraw = false;
        }
    }

    /// <summary>Records a command description after its Vulkan recording call succeeds.</summary>
    /// <param name="command">The command whose values are copied into the trace.</param>
    internal void RecordCommand(IVulkanCommand command)
    {
        if (!_enabled)
            return;

        ArgumentNullException.ThrowIfNull(command);
        lock (_lock)
        {
            if (!_firstDrawnFrameCommands.IsDefaultOrEmpty)
                return;

            _pendingCommands.Add(DescribeCommand(command));
            _pendingFrameHasDraw |= command is DrawCommand;
        }
    }

    /// <summary>Commits the pending trace when a frame containing a draw has been submitted successfully.</summary>
    internal void RecordFrameSubmitted()
    {
        if (!_enabled)
            return;

        lock (_lock)
        {
            _frameCount++;
            if (_firstDrawnFrameCommands.IsDefaultOrEmpty && _pendingFrameHasDraw)
            {
                _firstDrawnFrameCount = _frameCount;
                _firstDrawnFrameCommands = [.. _pendingCommands];
            }
            _pendingCommands.Clear();
            _pendingFrameHasDraw = false;
        }
    }

    /// <summary>
    /// Writes a compact summary of retained snapshots and the representative command sequence.
    /// Detailed snapshots remain available through <see cref="Snapshots"/>.
    /// </summary>
    public void Output()
    {
        if (!_enabled)
            return;

        PerformanceDiagnosticSnapshot[] snapshots;
        ImmutableArray<string> commandSequence;
        long frameCount;
        long firstDrawnFrameCount;
        lock (_lock)
        {
            snapshots = [.. _snapshots.Values];
            commandSequence = _firstDrawnFrameCommands;
            frameCount = _frameCount;
            firstDrawnFrameCount = _firstDrawnFrameCount;
        }

        if (
            frameCount == 0
            && snapshots.Length == 0
            && commandSequence.IsDefaultOrEmpty
        )
            return;

        var categorySummary = string.Join(
            ", ",
            snapshots
                .GroupBy(snapshot => snapshot.Category)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}={group.Count()}")
        );
        Debug.WriteLine(
            $"Vulkan Diagnostic Summary: FrameCount={frameCount}, Snapshots={snapshots.Length}, "
                + $"Drawables={snapshots.Select(snapshot => snapshot.DrawableId).Distinct().Count()}, "
                + $"Categories=[{categorySummary}]"
        );

        foreach (var snapshot in snapshots.Where(snapshot => snapshot.Category == "View"))
        {
            Debug.WriteLine(
                $"  View {snapshot.Label}: "
                    + $"Clip={GetSnapshotValue(snapshot, "EffectiveClippingRegion")}, "
                    + $"Selected={GetSnapshotValue(snapshot, "SelectedDrawableCount")}, "
                    + $"Batches={GetSnapshotValue(snapshot, "PreparedBatchCount")}, "
                    + $"Commands={GetSnapshotValue(snapshot, "PreparedCommandCount")}, "
                    + $"Skipped={GetSnapshotValue(snapshot, "RenderingSkipped")}"
            );
        }

        if (!commandSequence.IsDefaultOrEmpty)
        {
            Debug.WriteLine(
                $"  First submitted draw frame: FrameCount={firstDrawnFrameCount}, "
                    + $"Commands={commandSequence.Length}, "
                    + $"DrawCommands={commandSequence.Count(command => command.StartsWith("Draw ", StringComparison.Ordinal))}"
            );
        }
    }

    /// <summary>Gets a scalar value from a snapshot or reports it as unavailable.</summary>
    /// <param name="snapshot">The snapshot containing the value.</param>
    /// <param name="key">The value key to retrieve.</param>
    /// <returns>The stored value or <c>n/a</c>.</returns>
    private static string GetSnapshotValue(
        PerformanceDiagnosticSnapshot snapshot,
        string key
    ) => snapshot.Values.TryGetValue(key, out var value) ? value : "n/a";

    /// <summary>Formats a Vulkan command using values copied from its command data.</summary>
    /// <param name="command">The command that was recorded.</param>
    /// <returns>A readable command description.</returns>
    private static string DescribeCommand(IVulkanCommand command) =>
        command switch
        {
            BindPipelineCommand pipeline =>
                $"BindPipeline PipelineId={pipeline.PipelineId}, Pipeline={pipeline.Pipeline.Handle}",
            BindVertexBufferCommand vertex =>
                $"BindVertexBuffer Binding={vertex.Binding}, Buffer={vertex.Buffer.Handle}",
            BindDescriptorSetsCommand descriptors =>
                $"BindDescriptorSets PipelineId={descriptors.PipelineId}, Sets=[{string.Join(", ", descriptors.DescriptorSets.Select((set, index) => $"{index}:{set.Handle}"))}]",
            DrawCommand draw =>
                $"Draw DrawableId={draw.Drawable.Id}, Vertices={draw.VertexCount}, Instances={draw.InstanceCount}, FirstVertex={draw.FirstVertex}, FirstInstance={draw.FirstInstance}",
            SetViewportScissorCommand viewport =>
                $"SetViewportScissor Phase={viewport.RenderPassMask}, Area={viewport.RenderArea.Offset.X},{viewport.RenderArea.Offset.Y} {viewport.RenderArea.Extent.Width}x{viewport.RenderArea.Extent.Height}",
            UpdateUniformBufferCommand uniform =>
                $"UpdateUniformBuffer Buffer={uniform.BufferHandle}, Size={uniform.Data.Length}",
            _ =>
                $"{command.GetType().Name} DrawableId={command.Drawable?.Id.ToString() ?? "n/a"}, RenderPassMask={command.RenderPassMask}",
        };
}
