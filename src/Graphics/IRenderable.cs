namespace Nexus.Graphics.Components;

/// <summary>
/// Defines a graphics component that participates in the rendering process by supplying
/// render-layer membership and a packed instance record used to build render items.
/// </summary>
public interface IRenderable
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

    /// <summary>
    /// Gets the number of packed instance records contributed by this component.
    /// </summary>
    /// <remarks>
    /// Most renderable components contribute one record and need not implement this member.
    /// Components that expand into several instances can override it without exposing a
    /// collection as part of their public model.
    /// </remarks>
    int InstanceCount { get; }

    /// <summary>
    /// Gets the required size of, or writes, one of this component's packed instance records.
    /// </summary>
    /// <param name="instanceIndex">The zero-based component-local instance index.</param>
    /// <param name="destination">The destination, or an empty span when querying the size.</param>
    /// <returns>The required or written byte count.</returns>
    int GetInstanceData(int instanceIndex, Span<byte> destination);
}
