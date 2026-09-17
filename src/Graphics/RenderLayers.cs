namespace Nexus.Graphics;

public class RenderLayer(int index, string name, uint renderPassMask)
{
    public int Index { get; internal set; } = index;
    public string Name { get; } = name;
    public uint RenderPassMask { get; set; } = renderPassMask;
}

public class RenderLayers
{
    private readonly List<RenderLayer> _layers = [];

    public int Count => _layers.Count;

    public RenderLayer this[int index] => _layers[index];

    public RenderLayer Add(string name, uint renderPassMask)
    {
        var layer = new RenderLayer(index: _layers.Count, name, renderPassMask);

        _layers.Add(layer);
        return layer;
    }

    public void Remove(int index)
    {
        if (index < 0 || index >= _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _layers.RemoveAt(index);

        for (var i = index; i < _layers.Count; i++)
            _layers[i].Index = i;
    }

    public RenderLayers(int layerCount = 1)
    {
        if (layerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(layerCount));

        for (var i = 0; i < layerCount; i++)
            Add($"Layer {i}", RenderPasses.Main);
    }
}
