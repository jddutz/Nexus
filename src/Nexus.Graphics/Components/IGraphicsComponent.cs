namespace Nexus.Graphics.Components;

/// <summary>
/// Defines a component that supplies render-layer membership and a packed instance record.
/// </summary>
public interface IGraphicsComponent : IComponent
{
    /// <summary>
    /// Gets the render layers in which this component participates.
    /// </summary>
    IEnumerable<RenderLayer> RenderLayers { get; }

    /// <summary>
    /// Gets the required instance-record size or writes the record to a sufficiently sized destination.
    /// </summary>
    /// <param name="destination">The destination for the instance record, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    int GetInstanceData(Span<byte> destination);
}
