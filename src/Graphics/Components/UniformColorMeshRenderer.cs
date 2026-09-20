namespace Nexus.Graphics.Components;

/// <summary>
/// Renders geometry with a per-instance transformation matrix and uniform color.
/// </summary>
public class UniformColorMeshRenderer() : Component, IGraphicsComponent, IRenderable, IMeshInstance
{
    private static readonly int InstanceDataSize =
        System.Runtime.CompilerServices.Unsafe.SizeOf<Matrix4X4<float>>() + Marshal.SizeOf<Color>();

    private HashSet<RenderLayer> _renderLayers = [];
    private Mesh _mesh = BuiltInMesh.Empty;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;
    private Color _color = Colors.Black;

    /// <summary>
    /// Gets the render layers in which this component participates.
    /// </summary>
    public IEnumerable<RenderLayer> RenderLayers => _renderLayers;

    /// <summary>
    /// Gets the single renderable contribution produced by this component.
    /// </summary>
    public IReadOnlyList<IRenderable> Renderables => [this];

    /// <summary>Gets the number of packed instance records contributed by this component.</summary>
    public int InstanceCount => 1;

    /// <summary>
    /// Gets or sets the mesh rendered by this component.
    /// </summary>
    public Mesh Mesh
    {
        get => _mesh;
        set => SetProperty(ref _mesh, value);
    }

    /// <summary>
    /// Gets or sets the transformation matrix applied to the geometry.
    /// </summary>
    public Matrix4X4<float> TransformationMatrix
    {
        get => _transformationMatrix;
        set => SetProperty(ref _transformationMatrix, value);
    }

    /// <summary>
    /// Gets or sets the color applied to the rendered geometry.
    /// </summary>
    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

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

    /// <summary>
    /// Gets or writes the packed instance record at the specified component-local index.
    /// </summary>
    /// <param name="instanceIndex">The zero-based instance index.</param>
    /// <param name="destination">The destination span, or an empty span when querying its size.</param>
    /// <returns>The required or written byte count.</returns>
    public int GetInstanceData(int instanceIndex, Span<byte> destination)
    {
        if (instanceIndex != 0)
            throw new ArgumentOutOfRangeException(nameof(instanceIndex));

        return GetInstanceData(destination);
    }
}
