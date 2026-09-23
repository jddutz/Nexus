namespace Nexus.Graphics;

/// <summary>
/// Defines the collection contract for render layers.
/// </summary>
public interface IRenderLayerCollection
{
    /// <summary>
    /// Occurs when a render layer is added to the collection.
    /// </summary>
    event Action<IRenderLayer>? LayerAdded;

    /// <summary>
    /// Occurs when a render layer is removed from the collection.
    /// </summary>
    event Action<IRenderLayer>? LayerRemoved;

    /// <summary>
    /// Gets the number of occupied render-layer slots.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the render layer at the specified slot.
    /// </summary>
    /// <param name="index">The zero-based slot index.</param>
    /// <returns>The render layer at the slot, or <see langword="null"/> when it is not occupied.</returns>
    IRenderLayer? this[int index] { get; }

    /// <summary>
    /// Gets the render layers selected by the specified layer mask.
    /// </summary>
    /// <param name="renderLayerMask">The mask of layer slots to include.</param>
    /// <returns>The occupied render layers selected by the mask.</returns>
    IEnumerable<IRenderLayer> Get(ulong renderLayerMask);

    /// <summary>
    /// Creates a render layer in the first available slot.
    /// </summary>
    /// <param name="name">The name of the render layer.</param>
    /// <param name="renderPassMask">The render-pass mask for the render layer.</param>
    /// <returns>The created render layer, including its assigned slot index.</returns>
    IRenderLayer Create(string name, uint renderPassMask);

    /// <summary>
    /// Removes the render layer at the specified slot.
    /// </summary>
    /// <param name="index">The zero-based slot index.</param>
    void Remove(int index);

    /// <summary>
    /// Removes all render layers from the collection.
    /// </summary>
    void Clear();
}
