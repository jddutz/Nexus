namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>Sets dynamic viewport and scissor state for a view boundary.</summary>
public sealed unsafe class SetViewportScissorCommand : IVulkanCommand
{
    private readonly VkViewport _viewport;
    private readonly Rect2D _scissor;

    /// <summary>Creates a dynamic viewport and scissor command.</summary>
    /// <param name="renderPassMask">The Start or End phase in which the state is recorded.</param>
    /// <param name="x">The horizontal pixel offset.</param>
    /// <param name="y">The vertical pixel offset.</param>
    /// <param name="width">The viewport and scissor width in pixels.</param>
    /// <param name="height">The viewport and scissor height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">The phase is invalid or an extent is zero.</exception>
    public SetViewportScissorCommand(uint renderPassMask, int x, int y, uint width, uint height)
    {
        if (renderPassMask is not (RenderPasses.Start or RenderPasses.End))
            throw new ArgumentOutOfRangeException(nameof(renderPassMask));
        if (width == 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height == 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        RenderPassMask = renderPassMask;
        _viewport = new VkViewport
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        _scissor = new Rect2D { Offset = new Offset2D(x, y), Extent = new Extent2D(width, height) };
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
    public int RenderPriority => 0;

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
