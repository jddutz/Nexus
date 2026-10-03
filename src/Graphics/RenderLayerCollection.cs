namespace Nexus.Graphics;

/// <summary>
/// Stores render layers in slots addressable by a 64-bit layer mask.
/// </summary>
public class RenderLayerCollection : IRenderLayerCollection
{
    /// <summary>
    /// Gets the maximum number of render-layer slots supported by a 64-bit layer mask.
    /// </summary>
    public const int MaxLayers = 64;

    private readonly RenderLayer?[] _layers = new RenderLayer?[MaxLayers];

    /// <summary>
    /// Occurs when a render layer is added to the collection.
    /// </summary>
    public event Action<RenderLayer>? LayerAdded;

    /// <summary>
    /// Occurs when a render layer is removed from the collection.
    /// </summary>
    public event Action<RenderLayer>? LayerRemoved;

    /// <summary>
    /// Gets the number of occupied render-layer slots.
    /// </summary>
    public int Count => _layers.Count(layer => layer is not null);

    /// <summary>
    /// Gets the render layer at the specified slot.
    /// </summary>
    /// <param name="index">The zero-based slot index.</param>
    /// <returns>The layer at the slot.</returns>
    /// <exception cref="KeyNotFoundException">No layer exists at the specified slot.</exception>
    public RenderLayer? this[int index] =>
        _layers[index]
        ?? throw new KeyNotFoundException($"No render layer exists at slot {index}.");

    /// <summary>
    /// Gets the render layers selected by the specified layer mask.
    /// </summary>
    /// <param name="renderLayerMask">The mask of layer slots to include.</param>
    /// <returns>The occupied render layer slots selected by the mask.</returns>
    public IEnumerable<RenderLayer> Get(ulong renderLayerMask)
    {
        for (var index = 0; index < MaxLayers; index++)
        {
            if (_layers[index] is { } layer && (renderLayerMask & (1UL << index)) != 0)
                yield return layer;
        }
    }

    /// <summary>
    /// Creates a render layer in the first available slot.
    /// </summary>
    /// <param name="name">The name of the render layer.</param>
    /// <param name="renderPassMask">The render-pass mask for the layer.</param>
    /// <returns>The created render layer.</returns>
    /// <exception cref="InvalidOperationException">All render-layer slots are occupied.</exception>
    public RenderLayer Create(string name, uint renderPassMask)
    {
        var index = Array.FindIndex(_layers, layer => layer is null);
        if (index < 0)
            throw new InvalidOperationException(
                $"A maximum of {MaxLayers} render layers is supported."
            );

        var layer = new RenderLayer(index, name, renderPassMask);
        _layers[index] = layer;
        LayerAdded?.Invoke(layer);
        return layer;
    }

    /// <summary>
    /// Removes the render layer at the specified slot.
    /// </summary>
    /// <param name="index">The zero-based slot index.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the supported range.</exception>
    /// <exception cref="KeyNotFoundException">No layer exists at the specified slot.</exception>
    public void Remove(int index)
    {
        if (index < 0 || index >= MaxLayers)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_layers[index] is not { } layer)
            throw new KeyNotFoundException($"No render layer exists at slot {index}.");

        _layers[index] = null;
        LayerRemoved?.Invoke(layer);
    }

    /// <summary>
    /// Removes all render layers from the collection.
    /// </summary>
    public void Clear()
    {
        var removedLayers = _layers.OfType<RenderLayer>().ToArray();
        Array.Clear(_layers);

        foreach (var layer in removedLayers)
            LayerRemoved?.Invoke(layer);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderLayerCollection"/> class.
    /// </summary>
    /// <param name="layerCount">The number of default layers to create.</param>
    public RenderLayerCollection(int layerCount = 0)
    {
        if (layerCount < 0)
            throw new ArgumentOutOfRangeException(nameof(layerCount));

        for (var i = 0; i < layerCount; i++)
            Create($"Layer {i}", RenderPasses.Main);
    }
}
