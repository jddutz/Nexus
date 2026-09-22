namespace Nexus.Graphics.Vulkan.Commands;

public sealed unsafe class BeginRenderingCommand : IVulkanCommand
{
    private readonly Rect2D _renderArea;
    private readonly RenderingAttachmentInfo[] _colorAttachments;
    private readonly RenderingAttachmentInfo? _depthAttachment;
    private readonly RenderingAttachmentInfo? _stencilAttachment;
    private readonly uint _layerCount;
    private readonly uint _viewMask;
    private readonly RenderingFlags _flags;

    public BeginRenderingCommand(
        uint renderPass,
        Rect2D renderArea,
        IEnumerable<RenderingAttachmentInfo>? colorAttachments = null,
        RenderingAttachmentInfo? depthAttachment = null,
        RenderingAttachmentInfo? stencilAttachment = null,
        uint layerCount = 1,
        uint viewMask = 0,
        RenderingFlags flags = 0
    )
    {
        ArgumentOutOfRangeException.ThrowIfZero(layerCount);

        Id = Guid.NewGuid();
        RenderPass = renderPass;

        _renderArea = renderArea;
        _colorAttachments = colorAttachments?.ToArray() ?? [];
        _depthAttachment = depthAttachment;
        _stencilAttachment = stencilAttachment;
        _layerCount = layerCount;
        _viewMask = viewMask;
        _flags = flags;
    }

    public Guid Id { get; }

    public bool IsSticky => true;

    public RenderPhase Phase => RenderPhase.BeforeRenderPass;

    public uint RenderPass { get; }

    public PipelineId? PipelineId => null;

    public IDrawable? Drawable => null;

    public int RenderPriority => int.MinValue + 1;

    public bool IsRecorded { get; set; }

    public uint RefCount { get; set; } = 1;

    public void Record(Vk vk, CommandBuffer commandBuffer)
    {
        fixed (RenderingAttachmentInfo* colorAttachments = _colorAttachments)
        {
            var depthAttachment = _depthAttachment.GetValueOrDefault();
            var stencilAttachment = _stencilAttachment.GetValueOrDefault();

            var renderingInfo = new RenderingInfo
            {
                SType = StructureType.RenderingInfo,
                PNext = null,
                Flags = _flags,
                RenderArea = _renderArea,
                LayerCount = _layerCount,
                ViewMask = _viewMask,
                ColorAttachmentCount = (uint)_colorAttachments.Length,
                PColorAttachments = _colorAttachments.Length == 0 ? null : colorAttachments,
                PDepthAttachment = _depthAttachment.HasValue ? &depthAttachment : null,
                PStencilAttachment = _stencilAttachment.HasValue ? &stencilAttachment : null,
            };

            vk.CmdBeginRendering(commandBuffer, &renderingInfo);
        }

        IsRecorded = true;
    }
}
