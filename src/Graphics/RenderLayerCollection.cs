namespace Nexus.Graphics;

public class RenderLayerCollection : IRenderLayerCollection
{
    /// <summary>
    /// Gets the maximum number of render-layer slots supported by a 64-bit layer mask.
    /// </summary>
    public const int MaxLayers = 64;

    private readonly IRenderLayer?[] _layers = new IRenderLayer?[MaxLayers];

    public event Action<IRenderLayer>? LayerAdded;
    public event Action<IRenderLayer>? LayerRemoved;

    public int Count => _layers.Count(layer => layer is not null);

    public IRenderLayer? this[int index] =>
        _layers[index]
        ?? throw new KeyNotFoundException($"No render layer exists at slot {index}.");

    /// <summary>
    /// Gets the render layers selected by the specified layer mask.
    /// </summary>
    /// <param name="renderLayerMask">The mask of layer slots to include.</param>
    /// <returns>The occupied render layer slots selected by the mask.</returns>
    public IEnumerable<IRenderLayer> Get(ulong renderLayerMask)
    {
        for (var index = 0; index < MaxLayers; index++)
        {
            if (_layers[index] is { } layer && (renderLayerMask & (1UL << index)) != 0)
                yield return layer;
        }
    }

    public IRenderLayer Create(string name, uint renderPassMask)
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
        var removedLayers = _layers.Where(layer => layer is not null).ToArray();
        Array.Clear(_layers);

        foreach (var layer in removedLayers)
            LayerRemoved?.Invoke(layer!);
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
