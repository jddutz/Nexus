public interface IRenderable
{
    IVertexDataSource Vertices { get; }

    ITexture Texture { get; }

    IInstanceDataSource Instances { get; }

    /// <summary>
    /// Gets the packed uniform data required by the specified shader inputs.
    /// </summary>
    /// <param name="layout">The ordered uniform inputs required by a shader contract.</param>
    /// <returns>The packed uniform-buffer data.</returns>
    ReadOnlyMemory<byte> GetUniformData(ShaderInput[] layout);

    VertexShader VertexShader { get; }

    FragmentShader FragmentShader { get; }
}
