namespace Nexus.Graphics.Vulkan.Components;

/// <summary>
/// The realized Vulkan GPU state for a single active camera.
/// </summary>
/// <param name="ViewProjectionBuffer">The uniform buffer holding the camera's view-projection matrix.</param>
/// <param name="DescriptorSet">The set-0 descriptor set bound while drawing with this camera active.</param>
internal readonly record struct CameraRegistration(
    VkBuffer ViewProjectionBuffer,
    DescriptorSet DescriptorSet
);

/// <inheritdoc cref="ICameraRegistry" />
public class CameraRegistry(
    IBufferManager bufferManager,
    IDescriptorSetPool descriptorSetPool,
    IDescriptorSetLayoutFactory descriptorSetLayoutFactory,
    ILogger<CameraRegistry> logger
) : ICameraRegistry
{
    private static readonly ulong ViewProjectionSize = (ulong)Unsafe.SizeOf<Matrix4X4<float>>();

    private readonly IBufferManager _bufferManager = bufferManager;
    private readonly IDescriptorSetPool _descriptorSetPool = descriptorSetPool;
    private readonly IDescriptorSetLayoutFactory _descriptorSetLayoutFactory =
        descriptorSetLayoutFactory;
    private readonly ILogger<CameraRegistry> _logger = logger;
    private readonly Dictionary<ComponentId, CameraRegistration> _cameras = [];
    private ComponentId? _activeCameraId;
    private DescriptorSetLayout? _descriptorSetLayout;

    /// <inheritdoc />
    public DescriptorSet? ActiveCameraDescriptorSet =>
        _activeCameraId is { } id && _cameras.TryGetValue(id, out var registration)
            ? registration.DescriptorSet
            : null;

    /// <inheritdoc />
    public void Register(ICameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (_cameras.ContainsKey(camera.Id))
        {
            _activeCameraId = camera.Id;
            Update(camera);
            return;
        }

        var buffer = _bufferManager.CreateUniformBuffer(ViewProjectionSize);
        var descriptorSet = _descriptorSetPool.Allocate(GetOrCreateDescriptorSetLayout());

        _descriptorSetPool.WriteUniformBuffer(
            descriptorSet,
            binding: 0,
            buffer,
            offset: 0,
            range: ViewProjectionSize
        );

        _cameras[camera.Id] = new CameraRegistration(buffer, descriptorSet);
        _activeCameraId = camera.Id;

        UploadViewProjection(buffer, camera);

        _logger.LogDebug("Registered camera. ComponentId={ComponentId}", camera.Id);
    }

    /// <inheritdoc />
    public void Update(ICameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (!_cameras.TryGetValue(camera.Id, out var registration))
            return;

        UploadViewProjection(registration.ViewProjectionBuffer, camera);
    }

    /// <inheritdoc />
    public void Remove(ComponentId componentId)
    {
        if (!_cameras.Remove(componentId, out var registration))
            return;

        _descriptorSetPool.Release(registration.DescriptorSet);
        _bufferManager.DestroyBuffer(registration.ViewProjectionBuffer);

        if (_activeCameraId == componentId)
            _activeCameraId = null;

        _logger.LogDebug("Removed camera. ComponentId={ComponentId}", componentId);
    }

    /// <inheritdoc />
    public void Purge()
    {
        foreach (var registration in _cameras.Values)
        {
            _descriptorSetPool.Release(registration.DescriptorSet);
            _bufferManager.DestroyBuffer(registration.ViewProjectionBuffer);
        }

        _cameras.Clear();
        _activeCameraId = null;

        _logger.LogDebug("Purged all cameras.");
    }

    /// <summary>
    /// Copies the camera's current view-projection matrix into its uniform buffer.
    /// </summary>
    /// <param name="buffer">The uniform buffer to update.</param>
    /// <param name="camera">The camera supplying the matrix.</param>
    private void UploadViewProjection(VkBuffer buffer, ICameraComponent camera)
    {
        var viewProjection = camera.ViewProjectionMatrix;
        Span<byte> data = stackalloc byte[(int)ViewProjectionSize];
        MemoryMarshal.Write(data, in viewProjection);
        _bufferManager.UpdateBuffer(buffer, data);
    }

    /// <summary>
    /// Gets the set-0 descriptor-set layout shared by every registered camera, realizing it from
    /// <see cref="DescriptorSchemas.Camera"/> on first use.
    /// </summary>
    /// <returns>The camera descriptor-set layout.</returns>
    private DescriptorSetLayout GetOrCreateDescriptorSetLayout()
    {
        if (_descriptorSetLayout is { } existing)
            return existing;

        var layout = _descriptorSetLayoutFactory.Create(DescriptorSchemas.Camera)[0];
        _descriptorSetLayout = layout;

        return layout;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_descriptorSetLayout is { } layout)
        {
            _descriptorSetLayoutFactory.Destroy([layout]);
            _descriptorSetLayout = null;
        }

        GC.SuppressFinalize(this);
    }
}
