public interface IRenderable
{
    IVertexDataSource Vertices { get; }

    IInstanceDataSource Instances { get; }

    ITextureDataSource Texture { get; }

    VertexShader VertexShader { get; }

    FragmentShader FragmentShader { get; }
}
