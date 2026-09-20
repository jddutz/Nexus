namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// Creates and owns the native Vulkan resources backing sampled textures, keyed by
/// <see cref="Texture.Id"/>. Translates packed pixel data obtained from an
/// <see cref="ITexture"/> into a Vulkan image, image view, and sampler.
/// </summary>
/// <remarks>
/// This registry does not perform reference counting, usage tracking, or automatic eviction.
/// Callers are responsible for calling <see cref="Remove"/> when a texture is no longer needed,
/// and for disposing the registry to release all remaining textures.
/// </remarks>
/// <param name="context">The Vulkan context used to create and destroy texture resources.</param>
public sealed unsafe class TextureRegistry(Context context) : ITextureRegistry, IDisposable
{
    /// <summary>
    /// Native Vulkan handles backing a single registered texture.
    /// </summary>
    private readonly record struct TextureEntry(
        Image Image,
        DeviceMemory Memory,
        ImageView ImageView,
        Sampler Sampler,
        Format Format
    );

    private readonly Context _context = context;
    private readonly Dictionary<ContentId, TextureEntry> _textures = new();
    private readonly CommandPool _transientCommandPool = CreateTransientCommandPool(context);
    private bool _disposed;

    /// <summary>
    /// Gets whether a texture is currently registered for the given resource id.
    /// </summary>
    /// <param name="id">The resource id to look up.</param>
    /// <returns><see langword="true"/> if a texture is registered; otherwise, <see langword="false"/>.</returns>
    public bool IsRegistered(ContentId id) => _textures.ContainsKey(id);

    /// <summary>
    /// Registers a texture, creating its native Vulkan resources if it is not already registered.
    /// Requests packed pixel data from <paramref name="source"/> using <paramref name="format"/>.
    /// Does nothing if a texture is already registered for <paramref name="description"/>'s id.
    /// </summary>
    /// <param name="description">Describes the texture identity and dimensions.</param>
    /// <param name="source">Provides packed pixel data for the requested pixel format.</param>
    /// <param name="format">The pixel format used to request and upload pixel data.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="description"/> has invalid dimensions.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="format"/> has no corresponding Vulkan format.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the returned pixel data is empty, or its size matches neither a single pixel
    /// nor the texture's dimensions.
    /// </exception>
    public void Register(
        Texture description,
        ITexture source,
        ColorFormatEnum format = ColorFormatEnum.RGBA8UNorm
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_textures.ContainsKey(description.Id))
        {
            return;
        }

        if (description.Width == 0 || description.Height == 0)
        {
            throw new ArgumentException(
                $"Texture '{description.Id}' has invalid dimensions: {description.Width}x{description.Height}",
                nameof(description)
            );
        }

        var vulkanFormat = format.ToVulkanFormat();
        var bytesPerPixel = format.GetBytesPerPixel();
        var pixelData = source.GetPixelData(format);

        if (pixelData.Length == 0)
        {
            throw new InvalidOperationException(
                $"Texture '{description.Id}' source contains no pixel data."
            );
        }

        var expectedSize = checked(
            (int)((ulong)description.Width * description.Height * (ulong)bytesPerPixel)
        );

        // A single-pixel source is a solid color: allocate a 1x1 image and let sampling stretch it across the mesh.
        var isSolidColor = pixelData.Length == bytesPerPixel;

        if (!isSolidColor && pixelData.Length != expectedSize)
        {
            throw new InvalidOperationException(
                $"Texture '{description.Id}' pixel data length {pixelData.Length} does not match "
                    + $"the expected size {expectedSize} for format {format}."
            );
        }

        var imageWidth = isSolidColor ? 1u : description.Width;
        var imageHeight = isSolidColor ? 1u : description.Height;

        var image = CreateImage(imageWidth, imageHeight, vulkanFormat);
        var memory = AllocateAndBindImageMemory(image);

        UploadPixels(image, pixelData.Span, imageWidth, imageHeight, bytesPerPixel);
        TransitionImageLayout(
            image,
            ImageLayout.TransferDstOptimal,
            ImageLayout.ShaderReadOnlyOptimal
        );

        var imageView = CreateImageView(image, vulkanFormat);
        var sampler = CreateSampler();

        _textures[description.Id] = new TextureEntry(
            image,
            memory,
            imageView,
            sampler,
            vulkanFormat
        );
    }

    /// <summary>
    /// Attempts to retrieve the image view for a registered texture.
    /// </summary>
    /// <param name="id">The resource id of the texture.</param>
    /// <param name="imageView">The image view, if the texture is registered.</param>
    /// <returns><see langword="true"/> if the texture is registered; otherwise, <see langword="false"/>.</returns>
    public bool TryGetImageView(ContentId id, out ImageView imageView)
    {
        if (_textures.TryGetValue(id, out var entry))
        {
            imageView = entry.ImageView;
            return true;
        }

        imageView = default;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the sampler for a registered texture.
    /// </summary>
    /// <param name="id">The resource id of the texture.</param>
    /// <param name="sampler">The sampler, if the texture is registered.</param>
    /// <returns><see langword="true"/> if the texture is registered; otherwise, <see langword="false"/>.</returns>
    public bool TryGetSampler(ContentId id, out Sampler sampler)
    {
        if (_textures.TryGetValue(id, out var entry))
        {
            sampler = entry.Sampler;
            return true;
        }

        sampler = default;
        return false;
    }

    /// <summary>
    /// Destroys and removes a single registered texture. Safe to call for an id that is not
    /// registered, in which case no action is taken.
    /// </summary>
    /// <param name="id">The resource id of the texture to remove.</param>
    public void Remove(ContentId id)
    {
        if (_textures.Remove(id, out var entry))
        {
            DestroyEntry(entry);
        }
    }

    /// <summary>
    /// Destroys every remaining registered texture and releases the transient command pool.
    /// Safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var entry in _textures.Values)
        {
            DestroyEntry(entry);
        }
        _textures.Clear();

        _context.VulkanApi.DestroyCommandPool(_context.Device, _transientCommandPool, null);
    }

    /// <summary>
    /// Destroys the native Vulkan handles for a single texture entry in dependency order.
    /// </summary>
    /// <param name="entry">The texture entry to destroy.</param>
    private void DestroyEntry(TextureEntry entry)
    {
        if (entry.Sampler.Handle != 0)
        {
            _context.VulkanApi.DestroySampler(_context.Device, entry.Sampler, null);
        }
        if (entry.ImageView.Handle != 0)
        {
            _context.VulkanApi.DestroyImageView(_context.Device, entry.ImageView, null);
        }
        if (entry.Image.Handle != 0)
        {
            _context.VulkanApi.DestroyImage(_context.Device, entry.Image, null);
        }
        if (entry.Memory.Handle != 0)
        {
            _context.VulkanApi.FreeMemory(_context.Device, entry.Memory, null);
        }
    }

    /// <summary>
    /// Creates a 2D, single-mip, single-layer sampled Vulkan image sized for the given dimensions.
    /// </summary>
    /// <param name="width">The image width in texels.</param>
    /// <param name="height">The image height in texels.</param>
    /// <param name="format">The Vulkan format of the image.</param>
    /// <returns>The created image handle.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Vulkan fails to create the image.</exception>
    private Image CreateImage(uint width, uint height, Format format)
    {
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit,
        };

        Image image;
        var result = _context.VulkanApi.CreateImage(_context.Device, &imageInfo, null, &image);
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to create Vulkan image: {result}");
        }

        return image;
    }

    /// <summary>
    /// Allocates device-local memory sized for <paramref name="image"/> and binds it to the image.
    /// </summary>
    /// <param name="image">The image to allocate and bind memory for.</param>
    /// <returns>The allocated device memory.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Vulkan fails to allocate or bind memory.</exception>
    private DeviceMemory AllocateAndBindImageMemory(Image image)
    {
        _context.VulkanApi.GetImageMemoryRequirements(_context.Device, image, out var requirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(
                requirements.MemoryTypeBits,
                MemoryPropertyFlags.DeviceLocalBit
            ),
        };

        DeviceMemory memory;
        var result = _context.VulkanApi.AllocateMemory(_context.Device, &allocInfo, null, &memory);
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to allocate image memory: {result}");
        }

        _context.VulkanApi.BindImageMemory(_context.Device, image, memory, 0);

        return memory;
    }

    /// <summary>
    /// Uploads packed pixel data to an image via a temporary host-visible staging buffer, leaving
    /// the image in <see cref="ImageLayout.TransferDstOptimal"/> layout.
    /// </summary>
    /// <param name="image">The destination image, already bound to device memory.</param>
    /// <param name="pixelData">Packed pixel data sized <paramref name="width"/> x <paramref name="height"/> x <paramref name="bytesPerPixel"/>.</param>
    /// <param name="width">The image width in texels.</param>
    /// <param name="height">The image height in texels.</param>
    /// <param name="bytesPerPixel">The packed byte size of a single pixel.</param>
    private void UploadPixels(
        Image image,
        ReadOnlySpan<byte> pixelData,
        uint width,
        uint height,
        int bytesPerPixel
    )
    {
        var imageSize = (ulong)width * height * (ulong)bytesPerPixel;
        var stagingBuffer = CreateStagingBuffer(imageSize, out var stagingMemory);

        try
        {
            void* mapped;
            _context.VulkanApi.MapMemory(_context.Device, stagingMemory, 0, imageSize, 0, &mapped);
            pixelData.CopyTo(new Span<byte>(mapped, (int)imageSize));
            _context.VulkanApi.UnmapMemory(_context.Device, stagingMemory);

            TransitionImageLayout(image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
            CopyBufferToImage(stagingBuffer, image, width, height);
        }
        finally
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, stagingBuffer, null);
            _context.VulkanApi.FreeMemory(_context.Device, stagingMemory, null);
        }
    }

    /// <summary>
    /// Creates a host-visible, host-coherent buffer suitable as a transfer source for image uploads.
    /// </summary>
    /// <param name="size">The buffer capacity in bytes.</param>
    /// <param name="memory">The device memory backing the buffer.</param>
    /// <returns>The created staging buffer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Vulkan fails to create or allocate the buffer.</exception>
    private Silk.NET.Vulkan.Buffer CreateStagingBuffer(ulong size, out DeviceMemory memory)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive,
        };

        Silk.NET.Vulkan.Buffer buffer;
        var result = _context.VulkanApi.CreateBuffer(_context.Device, &bufferInfo, null, &buffer);
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to create staging buffer: {result}");
        }

        _context.VulkanApi.GetBufferMemoryRequirements(
            _context.Device,
            buffer,
            out var requirements
        );

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(
                requirements.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            ),
        };

        DeviceMemory stagingMemory;
        result = _context.VulkanApi.AllocateMemory(
            _context.Device,
            &allocInfo,
            null,
            &stagingMemory
        );
        if (result != Result.Success)
        {
            _context.VulkanApi.DestroyBuffer(_context.Device, buffer, null);
            throw new InvalidOperationException(
                $"Failed to allocate staging buffer memory: {result}"
            );
        }

        _context.VulkanApi.BindBufferMemory(_context.Device, buffer, stagingMemory, 0);

        memory = stagingMemory;
        return buffer;
    }

    /// <summary>
    /// Records and submits a one-time command buffer that copies a staging buffer into an image.
    /// </summary>
    /// <param name="buffer">The source staging buffer.</param>
    /// <param name="image">The destination image, in <see cref="ImageLayout.TransferDstOptimal"/> layout.</param>
    /// <param name="width">The image width in texels.</param>
    /// <param name="height">The image height in texels.</param>
    private void CopyBufferToImage(
        Silk.NET.Vulkan.Buffer buffer,
        Image image,
        uint width,
        uint height
    )
    {
        var commandBuffer = BeginSingleTimeCommands();

        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1),
        };

        _context.VulkanApi.CmdCopyBufferToImage(
            commandBuffer,
            buffer,
            image,
            ImageLayout.TransferDstOptimal,
            1,
            &region
        );

        EndSingleTimeCommands(commandBuffer);
    }

    /// <summary>
    /// Records and submits a one-time command buffer that transitions an image between the two
    /// layout pairs used by the upload pipeline.
    /// </summary>
    /// <param name="image">The image to transition.</param>
    /// <param name="oldLayout">The layout the image is currently in.</param>
    /// <param name="newLayout">The layout to transition the image to.</param>
    /// <exception cref="NotSupportedException">Thrown when the requested layout transition is not one of the two supported pairs.</exception>
    private void TransitionImageLayout(Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        var commandBuffer = BeginSingleTimeCommands();

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        PipelineStageFlags sourceStage;
        PipelineStageFlags destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (
            oldLayout == ImageLayout.TransferDstOptimal
            && newLayout == ImageLayout.ShaderReadOnlyOptimal
        )
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else
        {
            throw new NotSupportedException(
                $"Unsupported layout transition: {oldLayout} -> {newLayout}"
            );
        }

        _context.VulkanApi.CmdPipelineBarrier(
            commandBuffer,
            sourceStage,
            destinationStage,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier
        );

        EndSingleTimeCommands(commandBuffer);
    }

    /// <summary>
    /// Creates a 2D color image view covering the full mip and layer range of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The image to create a view for.</param>
    /// <param name="format">The Vulkan format of the image.</param>
    /// <returns>The created image view.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Vulkan fails to create the image view.</exception>
    private ImageView CreateImageView(Image image, Format format)
    {
        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        ImageView imageView;
        var result = _context.VulkanApi.CreateImageView(
            _context.Device,
            &viewInfo,
            null,
            &imageView
        );
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to create image view: {result}");
        }

        return imageView;
    }

    /// <summary>
    /// Creates a linear-filtered, edge-clamped sampler with no anisotropy or mip biasing.
    /// </summary>
    /// <returns>The created sampler.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Vulkan fails to create the sampler.</exception>
    private Sampler CreateSampler()
    {
        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            AnisotropyEnable = false,
            MaxAnisotropy = 1.0f,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 0.0f,
        };

        Sampler sampler;
        var result = _context.VulkanApi.CreateSampler(
            _context.Device,
            &samplerInfo,
            null,
            &sampler
        );
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Failed to create sampler: {result}");
        }

        return sampler;
    }

    /// <summary>
    /// Finds a physical-device memory type index that supports the requested properties.
    /// </summary>
    /// <param name="typeFilter">The bit mask of memory types supported by a Vulkan resource.</param>
    /// <param name="properties">The memory properties required by the resource.</param>
    /// <returns>The index of a compatible memory type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no compatible memory type is available.</exception>
    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.VulkanApi.GetPhysicalDeviceMemoryProperties(
            _context.PhysicalDevice,
            out var memProperties
        );

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if (
                (typeFilter & (1 << (int)i)) != 0
                && (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties
            )
            {
                return i;
            }
        }

        throw new InvalidOperationException("Failed to find suitable memory type");
    }

    /// <summary>
    /// Allocates a primary command buffer from the transient command pool and begins one-time
    /// recording.
    /// </summary>
    /// <returns>The command buffer, ready for recording.</returns>
    private CommandBuffer BeginSingleTimeCommands()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _transientCommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        CommandBuffer commandBuffer;
        _context.VulkanApi.AllocateCommandBuffers(_context.Device, &allocInfo, &commandBuffer);

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        _context.VulkanApi.BeginCommandBuffer(commandBuffer, &beginInfo);

        return commandBuffer;
    }

    /// <summary>
    /// Ends recording, submits, and synchronously waits for a one-time command buffer, then frees it.
    /// </summary>
    /// <param name="commandBuffer">The command buffer to end, submit, and free.</param>
    private void EndSingleTimeCommands(CommandBuffer commandBuffer)
    {
        _context.VulkanApi.EndCommandBuffer(commandBuffer);

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        _context.VulkanApi.QueueSubmit(_context.GraphicsQueue, 1, &submitInfo, default);
        _context.VulkanApi.QueueWaitIdle(_context.GraphicsQueue);

        _context.VulkanApi.FreeCommandBuffers(
            _context.Device,
            _transientCommandPool,
            1,
            &commandBuffer
        );
    }

    /// <summary>
    /// Creates the command pool used for short-lived, synchronously-submitted upload commands.
    /// </summary>
    /// <param name="context">The Vulkan context providing the device and graphics queue family.</param>
    /// <returns>The created transient command pool.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no graphics queue family is available or Vulkan fails to create the pool.</exception>
    private static CommandPool CreateTransientCommandPool(Context context)
    {
        var queueFamilyIndex =
            context.FindQueueFamily(QueueFlags.GraphicsBit)
            ?? throw new InvalidOperationException(
                "Failed to find a queue family that supports graphics commands."
            );

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamilyIndex,
            Flags = CommandPoolCreateFlags.TransientBit,
        };

        CommandPool pool;
        var result = context.VulkanApi.CreateCommandPool(context.Device, &poolInfo, null, &pool);
        if (result != Result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to create transient command pool: {result}"
            );
        }

        return pool;
    }
}
