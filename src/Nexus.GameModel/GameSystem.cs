namespace Nexus.GameModel;

/// <summary>
/// Provides the default implementation of the game system lifecycle.
/// </summary>
public class GameSystem(IGraphicsSystem graphics) : IGameSystem
{
    /// <summary>
    /// Gets or sets the identifier of the scene activated when the game starts.
    /// </summary>
    public GameObjectId InitialSceneId { get; set; } = new GameObjectId(1);

    /// <summary>
    /// Gets the currently active scene.
    /// </summary>
    public IScene? CurrentScene { get; private set; }

    /// <summary>
    /// Initializes the game system before the update loop begins.
    /// </summary>
    public void Initialize()
    {
        if (InitialSceneId == GameObjectId.Invalid)
            throw new InvalidOperationException("Initial Scene is not defined.");

        graphics.Load(BuiltInResource.UniformColorVertexShader);
        graphics.Load(BuiltInResource.UniformColorFragmentShader);
        graphics.Load(BuiltInResource.FullScreenTriangleMesh);

        CurrentScene =
            new Scene()
            ?? throw new InvalidOperationException($"Unable to load , {InitialSceneId}");
    }

    /// <summary>
    /// Updates the active scene for the elapsed time since the previous frame.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous frame.</param>
    public void Update(double deltaTime)
    {
        CurrentScene?.Update(deltaTime);
    }
}
