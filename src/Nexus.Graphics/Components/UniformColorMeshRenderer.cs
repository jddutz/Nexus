namespace Nexus.Graphics.Components;

/// <summary>
/// Renders geometry with a per-instance transformation matrix and uniform color.
/// </summary>
public class UniformColorMeshRenderer() : Component, IGraphicsComponent, IGeometryInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>() + Marshal.SizeOf<Color>();

    private HashSet<RenderLayer> _renderLayers = [];

    /// <summary>
    /// Gets the render layers in which this component participates.
    /// </summary>
    public IEnumerable<RenderLayer> RenderLayers => _renderLayers;

    /// <summary>
    /// Gets or sets the geometry rendered by this component.
    /// </summary>
    public IGeometry? Geometry { get; set; } = null;

    /// <summary>
    /// Gets or sets the transformation matrix applied to the geometry.
    /// </summary>
    public Matrix4X4<float> TransformationMatrix { get; set; } = Matrix4X4<float>.Identity;

    /// <summary>
    /// Gets or sets the color applied to the rendered geometry.
    /// </summary>
    public Color Color { get; set; } = Colors.Black;

    /// <summary>
    /// Gets the required instance-record size or writes the transform and color to a destination span.
    /// </summary>
    /// <param name="destination">The destination for the instance record, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    public int GetInstanceData(Span<byte> destination)
    {
        var transformationMatrixSize = System.Runtime.CompilerServices.Unsafe.SizeOf<
            Matrix4X4<float>
        >();

        if (destination.Length < InstanceDataSize)
            return InstanceDataSize;

        var transformationMatrix = TransformationMatrix;
        var color = Color;
        MemoryMarshal.Write(destination, in transformationMatrix);
        MemoryMarshal.Write(destination[transformationMatrixSize..], in color);

        return InstanceDataSize;
    }
}
