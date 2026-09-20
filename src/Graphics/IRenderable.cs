public interface IRenderable
{
    IVertexDataSource Vertices { get; }

    ITexture Texture { get; }

    IInstanceDataSource Instances { get; }

    VertexShader VertexShader { get; }

    FragmentShader FragmentShader { get; }
}
