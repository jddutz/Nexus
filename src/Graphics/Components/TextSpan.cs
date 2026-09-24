namespace Nexus.Graphics.Components;

/// <summary>
/// Renders a texture-mapped quad with a per-instance transformation matrix, source rectangle, and tint color.
/// </summary>
public class TextSpan : IDrawable, IInstanceDataSource, IMeshInstance
{
    public ulong RenderLayerMask => throw new NotImplementedException();

    public Mesh Mesh => throw new NotImplementedException();

    public ITexture Texture => throw new NotImplementedException();

    public ISamplingBehavior SamplingBehavior => throw new NotImplementedException();

    public IInstanceDataSource Instances => throw new NotImplementedException();

    public VertexShader? VertexShader => throw new NotImplementedException();

    public IShaderContract? TessellationControlShader => throw new NotImplementedException();

    public IShaderContract? TessellationEvalShader => throw new NotImplementedException();

    public IShaderContract? GeometryShader => throw new NotImplementedException();

    public FragmentShader? FragmentShader => throw new NotImplementedException();

    public ulong Count => throw new NotImplementedException();

    public Matrix4X4<float> TransformationMatrix => throw new NotImplementedException();

    public Color Color => throw new NotImplementedException();

    DrawableId IDrawable.Id => throw new NotImplementedException();

    DrawableId IInstanceDataSource.Id => throw new NotImplementedException();

    public ReadOnlyMemory<byte> GetInstanceData(ShaderInput[] layout)
    {
        throw new NotImplementedException();
    }

    public ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout)
    {
        throw new NotImplementedException();
    }
}
