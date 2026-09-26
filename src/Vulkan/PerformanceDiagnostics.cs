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
    private bool _pendingFrameHasDraw;

    /// <summary>Creates a diagnostics collector configured by the Vulkan options.</summary>
    /// <param name="options">The Vulkan settings; a directly constructed collector is enabled by default.</param>
    public PerformanceDiagnostics(IOptions<VulkanSettings>? options = null)
    {
        _enabled = options?.Value.EnableDiagnostics ?? true;
    }

    /// <summary>Gets whether diagnostic collection is enabled.</summary>
    public bool IsEnabled => _enabled;

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
            if (_firstDrawnFrameCommands.IsDefaultOrEmpty && _pendingFrameHasDraw)
                _firstDrawnFrameCommands = [.. _pendingCommands];
            _pendingCommands.Clear();
            _pendingFrameHasDraw = false;
        }
    }

    /// <summary>Writes the retained resource snapshots and representative command sequence to debug output.</summary>
    public void Output()
    {
        if (!_enabled)
            return;

        PerformanceDiagnosticSnapshot[] snapshots;
        ImmutableArray<string> commandSequence;
        lock (_lock)
        {
            snapshots = [.. _snapshots.Values];
            commandSequence = _firstDrawnFrameCommands;
        }

        if (snapshots.Length == 0 && commandSequence.IsDefaultOrEmpty)
            return;

        Debug.WriteLine("Vulkan Diagnostic Snapshot:");
        foreach (var group in snapshots.GroupBy(snapshot => snapshot.DrawableId))
        {
            Debug.WriteLine($"Drawable {group.Key}:");
            foreach (
                var snapshot in group
                    .OrderBy(snapshot => snapshot.Category)
                    .ThenBy(snapshot => snapshot.Label)
            )
            {
                Debug.WriteLine($"  {snapshot.Category} / {snapshot.Label}:");
                foreach (var value in snapshot.Values)
                    Debug.WriteLine($"    {value.Key}: {value.Value}");
                if (!snapshot.RawBytes.IsDefaultOrEmpty)
                    Debug.WriteLine(
                        $"    Bytes: {Convert.ToHexString(snapshot.RawBytes.AsSpan())}"
                    );
                foreach (var decodedValue in snapshot.DecodedValues)
                    Debug.WriteLine($"    {decodedValue}");
            }
        }

        if (!commandSequence.IsDefaultOrEmpty)
        {
            Debug.WriteLine("First successfully submitted frame containing a draw:");
            foreach (var command in commandSequence)
                Debug.WriteLine($"  {command}");
        }
    }

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
