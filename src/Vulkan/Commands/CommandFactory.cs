namespace Nexus.Graphics.Vulkan.Commands;

using Nexus.Graphics.Text;

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
/// <param name="diagnostics">Optional Vulkan diagnostic snapshot collector.</param>
public unsafe class CommandFactory(
    Context context,
    ISwapChain swapChain,
    IVertexBufferRegistry vertexBufferRegistry,
    IInstanceBufferRegistry instanceBufferRegistry,
    IImageRegistry imageRegistry,
    IPipelineRegistry pipelineRegistry,
    IDescriptorSetPool descriptorSetPool,
    IBufferManager bufferManager,
    ISamplerRegistry samplerRegistry,
    PerformanceDiagnostics? diagnostics = null
) : ICommandFactory
{
    private readonly Dictionary<DrawableId, DrawableAllocation> _allocations = [];
    private readonly PerformanceDiagnostics? _diagnostics = diagnostics;

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
        RecordDrawableSnapshot(drawable, pipelineDefinition, pipeline, pipelineLayout);
        RecordVertexBufferSnapshot(drawable, vertexBuffer, vertexShader.VertexFormat);
        RecordPipelineSnapshot(drawable, pipelineDefinition, pipeline, pipelineLayout);

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
                                setSchema.Set,
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
                        RecordUniformSnapshot(
                            drawable,
                            setSchema.Set,
                            descriptorSet,
                            binding.Binding,
                            uniformBuffer,
                            uniformData.Span,
                            DescribeUniformSemantic(vertexShader.UniformLayout),
                            "Initial"
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
            RecordInstanceBufferSnapshot(drawable, instanceBuffer, vertexShader.InstanceLayout);
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
            var instanceBuffer = instanceBufferRegistry.Get(drawable.Id);
            RecordInstanceBufferSnapshot(drawable, instanceBuffer, allocation.InstanceLayout);
            var instanceBinding = new BindVertexBufferCommand(
                RenderPasses.Main,
                allocation.PipelineId,
                drawable,
                1,
                instanceBuffer
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
            RecordUniformSnapshot(
                drawable,
                uniform.SetNumber,
                uniform.DescriptorSet,
                uniform.Binding,
                buffer,
                data.Span,
                DescribeUniformSemantic(layout),
                "Final"
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
    public IEnumerable<IVulkanCommand> CreateViewProjectionCommands(
        IDrawable drawable,
        Matrix4X4<float> viewProjectionMatrix
    )
    {
        ArgumentNullException.ThrowIfNull(drawable);

        var allocation = GetAllocation(drawable);
        var data = new byte[64];
        MemoryMarshal.Write(data.AsSpan(), in viewProjectionMatrix);

        foreach (var uniform in allocation.UniformBuffers)
        {
            if (
                uniform.Layout.Length != 1
                || uniform.Layout[0] is not { Semantic: InputSemantics.View, Size: 64 }
                || uniform.Capacity < (ulong)data.Length
            )
                continue;

            RecordUniformSnapshot(
                drawable,
                uniform.SetNumber,
                uniform.DescriptorSet,
                uniform.Binding,
                uniform.Buffer,
                data,
                "View",
                "Final"
            );
            yield return new UpdateUniformBufferCommand(uniform.Buffer, data);
        }
    }

    /// <summary>Captures drawable identity and shader intent without retaining the drawable.</summary>
    /// <param name="drawable">The drawable whose state is copied.</param>
    /// <param name="definition">The pipeline definition selected for the drawable.</param>
    /// <param name="pipeline">The realized Vulkan pipeline.</param>
    /// <param name="pipelineLayout">The realized Vulkan pipeline layout.</param>
    private void RecordDrawableSnapshot(
        IDrawable drawable,
        PipelineDefinition definition,
        Pipeline pipeline,
        PipelineLayout pipelineLayout
    )
    {
        if (_diagnostics?.IsEnabled != true)
            return;

        RecordDiagnostic(
            drawable.Id,
            "Drawable",
            "Identity",
            default,
            null,
            ("DrawableType", drawable.GetType().Name),
            ("RenderLayerMask", $"0x{drawable.RenderLayerMask:X}"),
            ("InstanceCount", drawable.InstanceCount.ToString()),
            ("MeshId", drawable.Mesh.Id.ToString()),
            ("PipelineId", definition.Id.ToString()),
            ("PipelineHandle", pipeline.Handle.ToString()),
            ("PipelineLayoutHandle", pipelineLayout.Handle.ToString()),
            ("VertexCount", drawable.Mesh.Count.ToString()),
            ("PrimitiveTopology", definition.Topology.ToString()),
            ("VertexShaderId", drawable.VertexShader?.Id.ToString() ?? "n/a"),
            ("VertexShaderName", drawable.VertexShader?.Name ?? "n/a"),
            ("FragmentShaderId", drawable.FragmentShader?.Id.ToString() ?? "n/a"),
            ("FragmentShaderName", drawable.FragmentShader?.Name ?? "n/a"),
            ("RenderPassMask", RenderPasses.Main.ToString())
        );
    }

    /// <summary>Copies the packed vertex bytes and a semantic-level decoding for the report.</summary>
    /// <param name="drawable">The drawable using the mesh.</param>
    /// <param name="buffer">The realized vertex buffer handle.</param>
    /// <param name="format">The format used to serialize the mesh.</param>
    private void RecordVertexBufferSnapshot(
        IDrawable drawable,
        VkBuffer buffer,
        VertexFormat format
    )
    {
        if (_diagnostics?.IsEnabled != true)
            return;

        var bytes = new byte[checked((int)drawable.Mesh.Count * (int)format.Stride)];
        drawable.Mesh.WriteTo(0, checked((int)drawable.Mesh.Count), format, bytes);
        var decoded = new List<string>();
        for (uint vertexIndex = 0; vertexIndex < drawable.Mesh.Count; vertexIndex++)
        {
            var vertexOffset = checked((int)vertexIndex * (int)format.Stride);
            var attributeOffset = 0;
            var attributes = new List<string>();
            foreach (var semantic in format.Inputs)
            {
                var byteCount = semantic switch
                {
                    VertexSemanticEnum.Position => format.PositionFormat == VectorFormatEnum.Float2D
                        ? 8
                        : 12,
                    VertexSemanticEnum.Normal => 12,
                    VertexSemanticEnum.TexCoord => 8,
                    VertexSemanticEnum.Color => checked((int)format.ColorFormat.GetBytesPerPixel()),
                    _ => 0,
                };
                var value = bytes.AsSpan(vertexOffset + attributeOffset, byteCount);
                var decodedValue =
                    semantic == VertexSemanticEnum.Color
                        ? DecodeColorAttribute(value, format.ColorFormat)
                        : DecodeFloatAttribute(value);
                attributes.Add($"{semantic}={decodedValue}");
                attributeOffset += byteCount;
            }
            decoded.Add($"[{vertexIndex}] {string.Join(" ", attributes)}");
        }

        RecordDiagnostic(
            drawable.Id,
            "Vertex Buffer",
            "Binding 0",
            bytes,
            decoded,
            ("BufferHandle", buffer.Handle.ToString()),
            ("Size", bytes.Length.ToString()),
            ("Stride", format.Stride.ToString()),
            ("VertexCount", drawable.Mesh.Count.ToString()),
            ("VertexFormatId", format.Id.ToString()),
            ("InputRate", VertexInputRate.Vertex.ToString())
        );
    }

    /// <summary>Copies packed instance bytes and decodes values according to the shader input layout.</summary>
    /// <param name="drawable">The drawable using the instance buffer.</param>
    /// <param name="buffer">The realized instance buffer handle.</param>
    /// <param name="layout">The ordered shader inputs packed into each instance.</param>
    private void RecordInstanceBufferSnapshot(
        IDrawable drawable,
        VkBuffer buffer,
        ShaderInput[] layout
    )
    {
        if (_diagnostics?.IsEnabled != true)
            return;

        var bytes = drawable.GetInstanceData(layout).ToArray();
        var stride = checked((uint)layout.Sum(input => (long)input.Size));
        var instanceCount = stride == 0 ? 0 : checked((int)(bytes.Length / (long)stride));
        var decoded = new List<string>();
        for (var instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
        {
            var offset = checked(instanceIndex * (int)stride);
            var values = new List<string>();
            foreach (var input in layout)
            {
                var inputBytes = bytes.AsSpan(offset, checked((int)input.Size));
                var value =
                    input.Semantic == InputSemantics.Transform && input.Size == 64
                        ? MemoryMarshal.Read<Matrix4X4<float>>(inputBytes).ToString()
                        : DecodeFloatAttribute(inputBytes);
                values.Add($"{DescribeSemantic(input.Semantic)}={value}");
                offset += checked((int)input.Size);
            }
            decoded.Add($"Instance[{instanceIndex}] {string.Join(" ", values)}");
        }

        RecordDiagnostic(
            drawable.Id,
            "Instance Buffer",
            "Binding 1",
            bytes,
            decoded,
            ("BufferHandle", buffer.Handle.ToString()),
            ("Size", bytes.Length.ToString()),
            ("Stride", stride.ToString()),
            ("InstanceCount", instanceCount.ToString()),
            ("InputRate", VertexInputRate.Instance.ToString())
        );
    }

    /// <summary>Captures pipeline handles, vertex input, fixed-function state, and shader contract identities.</summary>
    /// <param name="drawable">The drawable associated with the pipeline.</param>
    /// <param name="definition">The immutable pipeline configuration.</param>
    /// <param name="pipeline">The realized Vulkan pipeline.</param>
    /// <param name="pipelineLayout">The realized Vulkan pipeline layout.</param>
    private void RecordPipelineSnapshot(
        IDrawable drawable,
        PipelineDefinition definition,
        Pipeline pipeline,
        PipelineLayout pipelineLayout
    )
    {
        if (_diagnostics?.IsEnabled != true)
            return;

        var bindings = definition.VertexBindings.Select(binding =>
            $"Binding={binding.Binding}, Stride={binding.Stride}, InputRate={binding.InputRate}"
        );
        var attributes = definition.VertexAttributes.Select(attribute =>
            $"Location={attribute.Location}, Binding={attribute.Binding}, Format={attribute.Format}, Offset={attribute.Offset}, Semantic={DescribeVertexAttribute(drawable, attribute)}"
        );
        var decoded = bindings
            .Select(value => $"VertexBinding {value}")
            .Concat(attributes.Select(value => $"VertexAttribute {value}"));

        RecordDiagnostic(
            drawable.Id,
            "Pipeline",
            definition.Id.ToString(),
            default,
            decoded,
            ("PipelineId", definition.Id.ToString()),
            ("PipelineHandle", pipeline.Handle.ToString()),
            ("PipelineLayoutHandle", pipelineLayout.Handle.ToString()),
            ("Topology", definition.Topology.ToString()),
            ("CullMode", definition.CullMode.ToString()),
            ("FrontFace", definition.FrontFace.ToString()),
            ("PolygonMode", definition.PolygonMode.ToString()),
            ("RasterizerDiscardEnable", "False"),
            ("DepthTestEnable", definition.EnableDepthTest.ToString()),
            ("DepthWriteEnable", definition.EnableDepthWrite.ToString()),
            ("DepthCompareOp", definition.DepthCompareOp.ToString()),
            ("StencilTestEnable", "False"),
            ("BlendEnable", definition.EnableBlending.ToString()),
            ("SrcColorBlendFactor", definition.SrcBlendFactor.ToString()),
            ("DstColorBlendFactor", definition.DstBlendFactor.ToString()),
            ("ColorBlendOp", definition.BlendOp.ToString()),
            ("ColorWriteMask", "RGBA"),
            ("DynamicStates", "Viewport, Scissor"),
            ("ColorAttachmentFormat", swapChain.Format.ToString()),
            ("VertexShader", DescribeShader(definition.VertexShader)),
            ("FragmentShader", DescribeShader(definition.FragmentShader))
        );
    }

    /// <summary>Captures an immutable uniform payload and its descriptor-write identity.</summary>
    /// <param name="drawable">The drawable associated with the uniform.</param>
    /// <param name="setNumber">The descriptor-set index.</param>
    /// <param name="descriptorSet">The realized descriptor set.</param>
    /// <param name="binding">The descriptor binding.</param>
    /// <param name="buffer">The buffer referenced by the descriptor write.</param>
    /// <param name="data">The exact bytes uploaded to the buffer.</param>
    /// <param name="semantic">The meaning of the serialized uniform data.</param>
    /// <param name="stage">The upload stage label.</param>
    private void RecordUniformSnapshot(
        IDrawable drawable,
        uint setNumber,
        VkDescriptorSet descriptorSet,
        uint binding,
        VkBuffer buffer,
        ReadOnlySpan<byte> data,
        string semantic,
        string stage
    )
    {
        if (_diagnostics?.IsEnabled != true)
            return;

        var label = $"Set {setNumber} Binding {binding} {stage}";
        string[] decoded =
            semantic == "View" && data.Length == 64
                ? [$"Matrix={MemoryMarshal.Read<Matrix4X4<float>>(data).ToString()}"]
                : [$"Hex={Convert.ToHexString(data)}"];
        RecordDiagnostic(
            drawable.Id,
            "Uniform Buffer",
            label,
            data,
            decoded,
            ("Semantic", semantic),
            ("DescriptorSet", descriptorSet.Handle.ToString()),
            ("SetNumber", setNumber.ToString()),
            ("Binding", binding.ToString()),
            ("DescriptorType", DescriptorType.UniformBuffer.ToString()),
            ("BufferHandle", buffer.Handle.ToString()),
            ("Offset", "0"),
            ("Range", data.Length.ToString()),
            ("Size", data.Length.ToString())
        );
        RecordDiagnostic(
            drawable.Id,
            "Descriptor Write",
            $"Set {setNumber} Binding {binding}",
            default,
            null,
            ("DescriptorSet", descriptorSet.Handle.ToString()),
            ("SetNumber", setNumber.ToString()),
            ("Binding", binding.ToString()),
            ("DescriptorType", DescriptorType.UniformBuffer.ToString()),
            ("BufferHandle", buffer.Handle.ToString()),
            ("Offset", "0"),
            ("Range", data.Length.ToString())
        );
    }

    /// <param name="drawableId">The drawable identity.</param>
    /// <param name="category">The report category.</param>
    /// <param name="label">The item label within its category.</param>
    /// <param name="bytes">The bytes to copy into the snapshot.</param>
    /// <param name="decoded">Optional decoded values.</param>
    /// <param name="values">The scalar fields to copy into the snapshot.</param>
    private void RecordDiagnostic(
        DrawableId drawableId,
        string category,
        string label,
        ReadOnlySpan<byte> bytes,
        IEnumerable<string>? decoded,
        params (string Key, string Value)[] values
    )
    {
        if (_diagnostics?.IsEnabled != true)
            return;

        _diagnostics?.Record(
            new PerformanceDiagnosticSnapshot(
                drawableId,
                category,
                label,
                values.Select(value => KeyValuePair.Create(value.Key, value.Value)),
                bytes,
                decoded
            )
        );
    }

    /// <summary>Describes the uniform semantics represented by a shader layout.</summary>
    /// <param name="layout">The ordered uniform input layout.</param>
    /// <returns>The readable semantics in layout order.</returns>
    private static string DescribeUniformSemantic(ShaderInput[] layout) =>
        string.Join(
            ", ",
            layout.Select(input =>
                input.Semantic switch
                {
                    InputSemantics.Transform => "Transform",
                    InputSemantics.View => "View",
                    InputSemantics.Projection => "Projection",
                    InputSemantics.Color => "Color",
                    InputSemantics.TextureRegion => "TextureRegion",
                    _ => $"Semantic {input.Semantic}",
                }
            )
        );

    /// <summary>Formats a shader contract identity without retaining the contract instance.</summary>
    /// <param name="shader">The shader contract to describe.</param>
    /// <returns>The copied identity, source filename, and entry point.</returns>
    private static string DescribeShader(IShaderContract? shader) =>
        shader is null
            ? "n/a"
            : $"Id={shader.Id}, Name={shader.Name}, EntryPoint=main, Source={shader.SourceFileName}";

    /// <summary>Decodes an attribute-sized byte range as native-endian single-precision values.</summary>
    /// <param name="bytes">The copied attribute bytes.</param>
    /// <returns>A readable float tuple or hexadecimal representation.</returns>
    private static string DecodeFloatAttribute(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length % sizeof(float) != 0)
            return Convert.ToHexString(bytes);

        var values = new float[bytes.Length / sizeof(float)];
        for (var index = 0; index < values.Length; index++)
            values[index] = MemoryMarshal.Read<float>(bytes[(index * sizeof(float))..]);
        return $"({string.Join(",", values)})";
    }

    /// <summary>Names a known shader semantic or preserves its numeric identifier.</summary>
    /// <param name="semantic">The semantic identifier.</param>
    /// <returns>A readable semantic name.</returns>
    private static string DescribeSemantic(int semantic) =>
        semantic switch
        {
            InputSemantics.Transform => "Transform",
            InputSemantics.View => "View",
            InputSemantics.Projection => "Projection",
            InputSemantics.Color => "Color",
            InputSemantics.TextureRegion => "TextureRegion",
            _ => $"Semantic {semantic}",
        };

    /// <summary>Finds the semantic occupying a Vulkan vertex attribute using its binding and byte offset.</summary>
    /// <param name="drawable">The drawable providing the vertex and instance layouts.</param>
    /// <param name="attribute">The realized Vulkan attribute description.</param>
    /// <returns>The semantic name or <c>Unknown</c> when no layout matches.</returns>
    private static string DescribeVertexAttribute(
        IDrawable drawable,
        VertexInputAttributeDescription attribute
    )
    {
        if (drawable.VertexShader is not { } vertexShader)
            return "Unknown";

        if (attribute.Binding == 0)
        {
            uint offset = 0;
            foreach (var semantic in vertexShader.VertexFormat.Inputs)
            {
                if (offset == attribute.Offset)
                    return semantic.ToString();

                offset += semantic switch
                {
                    VertexSemanticEnum.Position => vertexShader.VertexFormat.PositionFormat
                    == VectorFormatEnum.Float2D
                        ? 8u
                        : 12u,
                    VertexSemanticEnum.Normal => 12u,
                    VertexSemanticEnum.Color => checked(
                        (uint)vertexShader.VertexFormat.ColorFormat.GetBytesPerPixel()
                    ),
                    VertexSemanticEnum.TexCoord => 8u,
                    _ => 0u,
                };
            }
        }
        else if (attribute.Binding == 1)
        {
            uint offset = 0;
            foreach (var input in vertexShader.InstanceLayout)
            {
                if (attribute.Offset >= offset && attribute.Offset < offset + input.Size)
                    return DescribeSemantic(input.Semantic);
                offset += input.Size;
            }
        }

        return "Unknown";
    }

    /// <summary>Decodes common normalized eight-bit colors and preserves other formats as bytes.</summary>
    /// <param name="bytes">The serialized color attribute.</param>
    /// <param name="format">The serialized color format.</param>
    /// <returns>The normalized channels or hexadecimal byte values.</returns>
    private static string DecodeColorAttribute(ReadOnlySpan<byte> bytes, ColorFormatEnum format)
    {
        if (format is ColorFormatEnum.RGBA8UNorm or ColorFormatEnum.RGB8UNorm)
            return $"({string.Join(",", bytes.ToArray().Select(channel => channel / 255f))})";

        return Convert.ToHexString(bytes);
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
        var updatedVertexBuffer = vertexBufferRegistry.Get(mesh.Id, vertexFormat.Id);
        RecordVertexBufferSnapshot(drawable, updatedVertexBuffer, vertexFormat);
        var vertexBinding = new BindVertexBufferCommand(
            RenderPasses.Main,
            allocation.PipelineId,
            drawable,
            0,
            updatedVertexBuffer
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
        RecordDrawableSnapshot(drawable, pipelineDefinition, pipeline, pipelineLayout);
        RecordPipelineSnapshot(drawable, pipelineDefinition, pipeline, pipelineLayout);
        RecordVertexBufferSnapshot(
            drawable,
            vertexBufferRegistry.Get(allocation.Mesh.Id, allocation.VertexFormat.Id),
            allocation.VertexFormat
        );
        if (allocation.InstanceLayout is not null)
            RecordInstanceBufferSnapshot(
                drawable,
                instanceBufferRegistry.Get(drawable.Id),
                allocation.InstanceLayout
            );

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

        if (drawable is TextSpan)
            pipelineDefinitionBuilder.WithBlending();

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
    /// <param name="SetNumber">The descriptor-set index containing the binding.</param>
    /// <param name="DescriptorSet">The descriptor set containing the binding.</param>
    /// <param name="Binding">The uniform-buffer binding index.</param>
    /// <param name="Buffer">The owned uniform buffer.</param>
    /// <param name="Capacity">The buffer capacity in bytes.</param>
    /// <param name="Layout">The shader inputs serialized into the buffer.</param>
    private sealed record UniformBufferAllocation(
        uint SetNumber,
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
