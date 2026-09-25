namespace Nexus.Graphics.Vulkan.Commands;

/// <summary>
/// Creates the Vulkan commands required to render a drawable.
/// </summary>
/// <param name="context">The Vulkan context used to create pipeline definitions.</param>
/// <param name="swapChain">The swap chain that provides render-pass handles.</param>
/// <param name="vertexBufferRegistry">The registry for mesh vertex buffers.</param>
/// <param name="instanceBufferRegistry">The registry for drawable instance buffers.</param>
/// <param name="imageRegistry">The registry for drawable images.</param>
/// <param name="pipelineRegistry">The registry for graphics pipelines.</param>
/// <param name="descriptorSetPool">The pool used to allocate and update descriptor sets.</param>
/// <param name="bufferManager">The manager for uniform buffers.</param>
/// <param name="samplerRegistry">The registry for texture samplers.</param>
public unsafe class CommandFactory(
    Context context,
    ISwapChain swapChain,
    IVertexBufferRegistry vertexBufferRegistry,
    IInstanceBufferRegistry instanceBufferRegistry,
    IImageRegistry imageRegistry,
    IPipelineRegistry pipelineRegistry,
    IDescriptorSetPool descriptorSetPool,
    IBufferManager bufferManager,
    ISamplerRegistry samplerRegistry
) : ICommandFactory
{
    private readonly Dictionary<DrawableId, DrawableAllocation> _allocations = [];

    /// <inheritdoc />
    public IEnumerable<IVulkanCommand> Create(IDrawable drawable)
    {
        /*
        // TODO: Can we get the input semantics from schema?
        foreach (var semantic in InputSemantics.All)
        {
            switch (semantic)
            {
                case InputSemantics.Transform:
                    throw new NotImplementedException();
                case InputSemantics.View:
                    throw new NotImplementedException();
                case InputSemantics.Projection:
                    throw new NotImplementedException();
                case InputSemantics.Color:
                    throw new NotImplementedException();
                case InputSemantics.TextureRegion:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }
        */
        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");

        var colorFormat =
            drawable.FragmentShader?.ColorFormat
            ?? throw new InvalidOperationException("Drawables must define a fragment shader.");

        if (_allocations.ContainsKey(drawable.Id))
            throw new InvalidOperationException(
                $"Drawable '{drawable.Id}' already has a Vulkan allocation."
            );

        var allocation = new DrawableAllocation(drawable);
        _allocations.Add(drawable.Id, allocation);

        var renderPassMask = RenderPasses.Main;

        vertexBufferRegistry.Create(drawable.Mesh, vertexShader.VertexFormat);
        allocation.Mesh = drawable.Mesh;
        allocation.VertexFormat = vertexShader.VertexFormat;
        allocation.VertexShader = vertexShader;
        if (vertexShader.InstanceLayout.Length > 0)
        {
            instanceBufferRegistry.Create(drawable, vertexShader.InstanceLayout);
            allocation.InstanceLayout = vertexShader.InstanceLayout;
        }

        foreach (var command in imageRegistry.Create(drawable.Texture, colorFormat))
        {
            yield return command;
        }

        allocation.Texture = drawable.Texture;
        allocation.ColorFormat = colorFormat;

        var pipelineDefinition = CreatePipelineDefinition(drawable, renderPassMask, vertexShader);

        var (pipeline, pipelineLayout) = pipelineRegistry.GetOrCreate(pipelineDefinition);
        allocation.PipelineId = pipelineDefinition.Id;
        allocation.Pipeline = pipeline;
        allocation.PipelineLayout = pipelineLayout;

        Debug.WriteLine(
            $"Prepared Vulkan drawable resources. DrawableId={drawable.Id}, PipelineId={pipelineDefinition.Id}"
        );

        var vertexBuffer = vertexBufferRegistry.Get(drawable.Mesh.Id, vertexShader.VertexFormat.Id);

        var bindPipelineCommand = new BindPipelineCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            pipeline
        );
        allocation.Commands.Add(bindPipelineCommand);
        yield return bindPipelineCommand;

        var schema = pipelineDefinition.DescriptorSchema.GetValueOrDefault();
        var descriptorSets = new VkDescriptorSet[schema.Sets.Length];

        foreach (var setSchema in schema.Sets)
        {
            var layout = pipelineRegistry.GetDescriptorSetLayout(
                pipelineDefinition.Id,
                setSchema.Set
            );

            var descriptorSet = descriptorSetPool.Allocate(layout);
            descriptorSets[setSchema.Set] = descriptorSet;
            allocation.DescriptorSets.Add(descriptorSet);

            Debug.WriteLine(
                $"Allocated descriptor set. DrawableId={drawable.Id}, PipelineId={pipelineDefinition.Id}, Set={setSchema.Set}"
            );

            foreach (var binding in setSchema.Bindings)
            {
                switch (binding.DescriptorType)
                {
                    case DescriptorType.UniformBuffer:
                        var uniformData = drawable.GetUniformData(vertexShader.UniformLayout);
                        var uniformBuffer = bufferManager.CreateUniformBuffer(
                            checked((ulong)uniformData.Length)
                        );
                        allocation.UniformBuffers.Add(
                            new UniformBufferAllocation(
                                descriptorSet,
                                binding.Binding,
                                uniformBuffer,
                                checked((ulong)uniformData.Length),
                                vertexShader.UniformLayout
                            )
                        );
                        bufferManager.UpdateBuffer(uniformBuffer, uniformData.Span);
                        descriptorSetPool.WriteUniformBuffer(
                            descriptorSet,
                            binding.Binding,
                            uniformBuffer,
                            0,
                            checked((ulong)uniformData.Length)
                        );
                        break;

                    case DescriptorType.CombinedImageSampler:
                        samplerRegistry.Create(drawable.SamplingBehavior);
                        allocation.ImageSamplerBindings.Add(
                            new ImageSamplerAllocation(
                                descriptorSet,
                                binding.Binding,
                                drawable.SamplingBehavior
                            )
                        );
                        descriptorSetPool.WriteCombinedImageSampler(
                            descriptorSet,
                            binding.Binding,
                            imageRegistry.Get(drawable.Texture, colorFormat),
                            samplerRegistry.Get(drawable.SamplingBehavior.Id)
                        );
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Descriptor type {binding.DescriptorType} is not supported."
                        );
                }
            }
        }

        var bindDescriptorSetsCommand = new BindDescriptorSetsCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            pipelineLayout,
            descriptorSets
        );
        allocation.Commands.Add(bindDescriptorSetsCommand);
        yield return bindDescriptorSetsCommand;

        var bindVertexBufferCommand = new BindVertexBufferCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            0,
            vertexBuffer
        );
        allocation.Commands.Add(bindVertexBufferCommand);
        yield return bindVertexBufferCommand;

        if (vertexShader.InstanceLayout.Length > 0)
        {
            var instanceBuffer = instanceBufferRegistry.Get(drawable.Id);
            var bindInstanceBufferCommand = new BindVertexBufferCommand(
                renderPassMask,
                pipelineDefinition.Id,
                drawable,
                1,
                instanceBuffer
            );
            allocation.Commands.Add(bindInstanceBufferCommand);
            yield return bindInstanceBufferCommand;
        }

        var drawCommand = new DrawCommand(
            renderPassMask,
            pipelineDefinition.Id,
            drawable,
            checked((uint)drawable.Mesh.Count),
            checked((uint)drawable.InstanceCount)
        );
        allocation.Commands.Add(drawCommand);
        yield return drawCommand;

        Debug.WriteLine(
            $"Created Vulkan commands. DrawableId={drawable.Id}, PipelineId={pipelineDefinition.Id}, VertexCount={drawable.Mesh.Count}"
        );
    }

    /// <inheritdoc />
    public IEnumerable<IVulkanCommand> UpdateInstanceData(IDrawable drawable)
    {
        var allocation = GetAllocation(drawable);
        if (allocation.InstanceLayout is not null)
            instanceBufferRegistry.Create(drawable, allocation.InstanceLayout);

        var updatedCommands = new List<IVulkanCommand>();
        if (allocation.InstanceLayout is not null)
        {
            var instanceBinding = new BindVertexBufferCommand(
                RenderPasses.Main,
                allocation.PipelineId,
                drawable,
                1,
                instanceBufferRegistry.Get(drawable.Id)
            );
            StoreCommand(allocation, instanceBinding);
            updatedCommands.Add(instanceBinding);
        }

        var drawCommand = CreateDrawCommand(allocation);
        StoreCommand(allocation, drawCommand);
        updatedCommands.Add(drawCommand);
        return updatedCommands;
    }

    /// <inheritdoc />
    public IEnumerable<IVulkanCommand> UpdateUniformData(IDrawable drawable)
    {
        var allocation = GetAllocation(drawable);

        for (var index = 0; index < allocation.UniformBuffers.Count; index++)
        {
            var uniform = allocation.UniformBuffers[index];
            var layout = allocation.VertexShader.UniformLayout;
            var data = drawable.GetUniformData(layout);
            if (data.IsEmpty)
                throw new InvalidOperationException("Uniform data cannot be empty.");

            var buffer = uniform.Buffer;
            var capacity = uniform.Capacity;
            if ((ulong)data.Length > capacity)
            {
                buffer = bufferManager.CreateUniformBuffer(checked((ulong)data.Length));
                capacity = checked((ulong)data.Length);
            }

            bufferManager.UpdateBuffer(buffer, data.Span);
            descriptorSetPool.WriteUniformBuffer(
                uniform.DescriptorSet,
                uniform.Binding,
                buffer,
                0,
                checked((ulong)data.Length)
            );

            if (buffer.Handle != uniform.Buffer.Handle)
                bufferManager.DestroyBuffer(uniform.Buffer);

            allocation.UniformBuffers[index] = uniform with
            {
                Buffer = buffer,
                Capacity = capacity,
                Layout = layout,
            };
        }

        return [];
    }

    /// <inheritdoc />
    public IEnumerable<IVulkanCommand> UpdateTexture(IDrawable drawable)
    {
        var allocation = GetAllocation(drawable);
        var texture = drawable.Texture;
        var colorFormat =
            drawable.FragmentShader?.ColorFormat
            ?? throw new InvalidOperationException("Drawables must define a fragment shader.");
        var uploadCommands = new List<IVulkanCommand>();

        if (texture.Id == allocation.Texture.Id && colorFormat == allocation.ColorFormat)
        {
            if (!ReferenceEquals(texture, allocation.Texture))
                uploadCommands.AddRange(imageRegistry.Update(texture, colorFormat));
        }
        else
        {
            uploadCommands.AddRange(imageRegistry.Create(texture, colorFormat));
            uploadCommands.AddRange(
                imageRegistry.Release(allocation.Texture, allocation.ColorFormat)
            );
        }

        var samplingBehavior = drawable.SamplingBehavior;
        for (var index = 0; index < allocation.ImageSamplerBindings.Count; index++)
        {
            var binding = allocation.ImageSamplerBindings[index];
            if (binding.SamplingBehavior.Id != samplingBehavior.Id)
            {
                samplerRegistry.Create(samplingBehavior);
                samplerRegistry.Release(binding.SamplingBehavior);
                allocation.ImageSamplerBindings[index] = binding with
                {
                    SamplingBehavior = samplingBehavior,
                };
            }

            descriptorSetPool.WriteCombinedImageSampler(
                binding.DescriptorSet,
                binding.Binding,
                imageRegistry.Get(texture, colorFormat),
                samplerRegistry.Get(samplingBehavior.Id)
            );
        }

        allocation.Texture = texture;
        allocation.ColorFormat = colorFormat;
        return uploadCommands;
    }

    /// <inheritdoc />
    public IEnumerable<IVulkanCommand> UpdateMesh(IDrawable drawable)
    {
        var allocation = GetAllocation(drawable);
        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");
        var mesh = drawable.Mesh;
        var vertexFormat = vertexShader.VertexFormat;

        if (mesh.Id == allocation.Mesh.Id && vertexFormat.Id == allocation.VertexFormat.Id)
            vertexBufferRegistry.Update(mesh, vertexFormat);
        else
        {
            vertexBufferRegistry.Create(mesh, vertexFormat);
            vertexBufferRegistry.Release(allocation.Mesh, allocation.VertexFormat);
        }

        allocation.Mesh = mesh;
        allocation.VertexFormat = vertexFormat;
        var vertexBinding = new BindVertexBufferCommand(
            RenderPasses.Main,
            allocation.PipelineId,
            drawable,
            0,
            vertexBufferRegistry.Get(mesh.Id, vertexFormat.Id)
        );
        StoreCommand(allocation, vertexBinding);
        return [vertexBinding];
    }

    /// <inheritdoc />
    public IEnumerable<IVulkanCommand> UpdateShaders(IDrawable drawable)
    {
        var allocation = GetAllocation(drawable);
        var vertexShader =
            drawable.VertexShader
            ?? throw new InvalidOperationException("Drawables must define a vertex shader.");
        var colorFormat =
            drawable.FragmentShader?.ColorFormat
            ?? throw new InvalidOperationException("Drawables must define a fragment shader.");
        var pipelineDefinition = CreatePipelineDefinition(
            drawable,
            RenderPasses.Main,
            vertexShader
        );
        var oldPipelineId = allocation.PipelineId;

        if (vertexShader.VertexFormat.Id != allocation.VertexFormat.Id)
        {
            vertexBufferRegistry.Create(drawable.Mesh, vertexShader.VertexFormat);
            vertexBufferRegistry.Release(allocation.Mesh, allocation.VertexFormat);
            allocation.Mesh = drawable.Mesh;
            allocation.VertexFormat = vertexShader.VertexFormat;
        }

        if (!vertexShader.InstanceLayout.SequenceEqual(allocation.InstanceLayout ?? []))
        {
            if (vertexShader.InstanceLayout.Length == 0)
                instanceBufferRegistry.Release(drawable.Id);
            else
                instanceBufferRegistry.Create(drawable, vertexShader.InstanceLayout);

            allocation.InstanceLayout =
                vertexShader.InstanceLayout.Length == 0 ? null : vertexShader.InstanceLayout;
        }

        var (pipeline, pipelineLayout) =
            pipelineDefinition.Id == oldPipelineId
                ? (pipelineRegistry.Get(oldPipelineId), pipelineRegistry.GetLayout(oldPipelineId))
                : pipelineRegistry.GetOrCreate(pipelineDefinition);
        allocation.VertexShader = vertexShader;
        allocation.PipelineId = pipelineDefinition.Id;
        allocation.Pipeline = pipeline;
        allocation.PipelineLayout = pipelineLayout;

        var uploadCommands = new List<IVulkanCommand>();
        if (colorFormat != allocation.ColorFormat)
        {
            uploadCommands.AddRange(imageRegistry.Create(allocation.Texture, colorFormat));
            imageRegistry.Release(allocation.Texture, allocation.ColorFormat);
            allocation.ColorFormat = colorFormat;
        }

        UpdateUniformData(drawable);
        foreach (var binding in allocation.ImageSamplerBindings)
        {
            descriptorSetPool.WriteCombinedImageSampler(
                binding.DescriptorSet,
                binding.Binding,
                imageRegistry.Get(allocation.Texture, allocation.ColorFormat),
                samplerRegistry.Get(binding.SamplingBehavior.Id)
            );
        }

        if (oldPipelineId != allocation.PipelineId)
            pipelineRegistry.Release(oldPipelineId);

        return uploadCommands.Concat(RebuildCommands(allocation));
    }

    /// <inheritdoc />
    public IEnumerable<IVulkanCommand> Release(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);

        if (!_allocations.Remove(drawable.Id, out var allocation))
            return [];

        foreach (var descriptorSet in allocation.DescriptorSets)
            descriptorSetPool.Release(descriptorSet);

        foreach (var uniformBuffer in allocation.UniformBuffers)
            bufferManager.DestroyBuffer(uniformBuffer.Buffer);

        foreach (var binding in allocation.ImageSamplerBindings)
            samplerRegistry.Release(binding.SamplingBehavior);

        pipelineRegistry.Release(allocation.PipelineId);
        if (allocation.InstanceLayout is not null)
            instanceBufferRegistry.Release(drawable.Id);
        vertexBufferRegistry.Release(allocation.Mesh, allocation.VertexFormat);

        return imageRegistry.Release(allocation.Texture, allocation.ColorFormat);
    }

    /// <summary>Gets the active factory allocation for a drawable.</summary>
    /// <param name="drawable">The drawable whose allocation is required.</param>
    /// <returns>The retained drawable allocation.</returns>
    private DrawableAllocation GetAllocation(IDrawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return _allocations.TryGetValue(drawable.Id, out var allocation)
            ? allocation
            : throw new InvalidOperationException(
                $"Drawable '{drawable.Id}' has no Vulkan allocation."
            );
    }

    /// <summary>Rebuilds all persistent commands after pipeline state changes.</summary>
    /// <param name="allocation">The updated drawable allocation.</param>
    /// <returns>The rebuilt persistent commands.</returns>
    private IReadOnlyList<IVulkanCommand> RebuildCommands(DrawableAllocation allocation)
    {
        var commands = new List<IVulkanCommand>
        {
            new BindPipelineCommand(
                RenderPasses.Main,
                allocation.PipelineId,
                allocation.Drawable,
                allocation.Pipeline
            ),
            new BindDescriptorSetsCommand(
                RenderPasses.Main,
                allocation.PipelineId,
                allocation.Drawable,
                allocation.PipelineLayout,
                allocation.DescriptorSets
            ),
            new BindVertexBufferCommand(
                RenderPasses.Main,
                allocation.PipelineId,
                allocation.Drawable,
                0,
                vertexBufferRegistry.Get(allocation.Mesh.Id, allocation.VertexFormat.Id)
            ),
        };

        if (allocation.InstanceLayout is not null)
            commands.Add(
                new BindVertexBufferCommand(
                    RenderPasses.Main,
                    allocation.PipelineId,
                    allocation.Drawable,
                    1,
                    instanceBufferRegistry.Get(allocation.Drawable.Id)
                )
            );

        commands.Add(CreateDrawCommand(allocation));

        allocation.Commands.Clear();
        allocation.Commands.AddRange(commands);
        return commands;
    }

    /// <summary>Creates a draw command from the drawable's current geometry and instance count.</summary>
    /// <param name="allocation">The active drawable allocation.</param>
    /// <returns>The draw command for the current drawable state.</returns>
    private static DrawCommand CreateDrawCommand(DrawableAllocation allocation) =>
        new(
            RenderPasses.Main,
            allocation.PipelineId,
            allocation.Drawable,
            checked((uint)allocation.Drawable.Mesh.Count),
            checked((uint)allocation.Drawable.InstanceCount)
        );

    /// <summary>Replaces the retained command occupying the same drawable command slot.</summary>
    /// <param name="allocation">The active drawable allocation.</param>
    /// <param name="command">The replacement command.</param>
    private static void StoreCommand(DrawableAllocation allocation, IVulkanCommand command)
    {
        allocation.Commands.RemoveAll(existing => IsSameCommandSlot(existing, command));
        allocation.Commands.Add(command);
    }

    /// <summary>Determines whether two commands occupy the same per-drawable binding slot.</summary>
    /// <param name="left">The existing command.</param>
    /// <param name="right">The replacement command.</param>
    /// <returns><see langword="true"/> when the replacement supersedes the existing command.</returns>
    private static bool IsSameCommandSlot(IVulkanCommand left, IVulkanCommand right)
    {
        if (left.GetType() != right.GetType())
            return false;

        return left is not BindVertexBufferCommand leftBinding
            || right is not BindVertexBufferCommand rightBinding
            || leftBinding.Binding == rightBinding.Binding;
    }

    /// <summary>
    /// Creates the pipeline definition used by a drawable.
    /// </summary>
    /// <param name="drawable">The drawable whose shader state defines the pipeline.</param>
    /// <param name="renderPassMask">The render-pass mask used by the drawable.</param>
    /// <param name="vertexShader">The drawable's required vertex shader.</param>
    /// <returns>The pipeline definition for the drawable.</returns>
    protected virtual PipelineDefinition CreatePipelineDefinition(
        IDrawable drawable,
        uint renderPassMask,
        VertexShader vertexShader
    )
    {
        var pipelineDefinitionBuilder = new PipelineDefinitionBuilder(
            drawable.GetType().Name,
            context
        )
            .WithShader(vertexShader)
            .WithDescriptorSchema(DescriptorSchemas.Textured)
            .WithRenderPass(swapChain.Passes[RenderPasses.GetIndex(renderPassMask)]);

        if (drawable.TessellationControlShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationControlShader);
        if (drawable.TessellationEvalShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.TessellationEvalShader);
        if (drawable.GeometryShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.GeometryShader);
        if (drawable.FragmentShader is not null)
            pipelineDefinitionBuilder.WithShader(drawable.FragmentShader);

        return BuildPipelineDefinition(pipelineDefinitionBuilder);
    }

    /// <summary>
    /// Builds a configured pipeline definition.
    /// </summary>
    /// <param name="builder">The configured pipeline definition builder.</param>
    /// <returns>The immutable pipeline definition.</returns>
    protected virtual PipelineDefinition BuildPipelineDefinition(
        PipelineDefinitionBuilder builder
    ) => builder.Build();

    /// <summary>Retains the buffer and descriptor metadata for a uniform binding.</summary>
    /// <param name="DescriptorSet">The descriptor set containing the binding.</param>
    /// <param name="Binding">The uniform-buffer binding index.</param>
    /// <param name="Buffer">The owned uniform buffer.</param>
    /// <param name="Capacity">The buffer capacity in bytes.</param>
    /// <param name="Layout">The shader inputs serialized into the buffer.</param>
    private sealed record UniformBufferAllocation(
        DescriptorSet DescriptorSet,
        uint Binding,
        VkBuffer Buffer,
        ulong Capacity,
        ShaderInput[] Layout
    );

    /// <summary>Retains the descriptor and sampler metadata for an image binding.</summary>
    /// <param name="DescriptorSet">The descriptor set containing the binding.</param>
    /// <param name="Binding">The combined-image-sampler binding index.</param>
    /// <param name="SamplingBehavior">The sampler behavior referenced by the binding.</param>
    private sealed record ImageSamplerAllocation(
        DescriptorSet DescriptorSet,
        uint Binding,
        ISamplingBehavior SamplingBehavior
    );

    /// <summary>Owns the Vulkan resources acquired for one active drawable.</summary>
    /// <param name="drawable">The drawable whose allocation is retained.</param>
    private sealed class DrawableAllocation(IDrawable drawable)
    {
        public IDrawable Drawable { get; } = drawable;
        public Mesh Mesh = null!;
        public VertexFormat VertexFormat = null!;
        public VertexShader VertexShader = null!;
        public ShaderInput[]? InstanceLayout;
        public ITexture Texture = null!;
        public ColorFormatEnum ColorFormat;
        public PipelineId PipelineId;
        public Pipeline Pipeline;
        public PipelineLayout PipelineLayout;
        public List<DescriptorSet> DescriptorSets { get; } = [];
        public List<UniformBufferAllocation> UniformBuffers { get; } = [];
        public List<ImageSamplerAllocation> ImageSamplerBindings { get; } = [];
        public List<IVulkanCommand> Commands { get; } = [];
    }
}
