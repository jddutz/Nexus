namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>Sets dynamic viewport and scissor state for a view boundary.</summary>
public sealed unsafe class SetViewportScissorCommand : IVulkanCommand
{
    private readonly VkViewport _viewport;
    private readonly Rect2D _scissor;

    /// <summary>Creates a dynamic viewport and scissor command.</summary>
    /// <param name="renderPassMask">The Start or End phase in which the state is recorded.</param>
    /// <param name="viewport">The projection viewport rectangle.</param>
    /// <param name="scissor">The clipping/scissor rectangle.</param>
    /// <exception cref="ArgumentOutOfRangeException">The phase is invalid or an extent is zero.</exception>
    public SetViewportScissorCommand(
        uint renderPassMask,
        Rectangle<int> viewport,
        Rectangle<int> scissor
    )
    {
        if (renderPassMask is not (RenderPasses.Start or RenderPasses.End))
            throw new ArgumentOutOfRangeException(nameof(renderPassMask));
        if (viewport.Size.X <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewport));
        if (viewport.Size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewport));
        if (scissor.Size.X <= 0)
            throw new ArgumentOutOfRangeException(nameof(scissor));
        if (scissor.Size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(scissor));

        RenderPassMask = renderPassMask;
        _viewport = new VkViewport
        {
            X = viewport.Origin.X,
            Y = viewport.Origin.Y,
            Width = (uint)viewport.Size.X,
            Height = (uint)viewport.Size.Y,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        _scissor = new Rect2D
        {
            Offset = new Offset2D(scissor.Origin.X, scissor.Origin.Y),
            Extent = new Extent2D((uint)scissor.Size.X, (uint)scissor.Size.Y),
        };
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public bool IsSticky => false;

    /// <inheritdoc />
    public uint RenderPassMask { get; }

    /// <inheritdoc />
    public PipelineId? PipelineId => null;

    /// <inheritdoc />
    public IDrawable? Drawable => null;

    /// <inheritdoc />
    public long RenderPriority => 0;

    /// <summary>Gets the scissor rectangle used as the view's dynamic-rendering area.</summary>
    public Rect2D RenderArea => _scissor;

    /// <inheritdoc />
    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        var viewport = _viewport;
        var scissor = _scissor;
        vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
    }
}
