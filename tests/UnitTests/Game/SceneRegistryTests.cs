namespace Tests;

using Nexus.Game;

/// <summary>
/// Verifies scenes are created through factories registered by identifier.
/// </summary>
public class SceneRegistryTests
{
    /// <summary>
    /// Verifies a registered factory is used and unknown identifiers are not loaded.
    /// </summary>
    [Fact]
    public void Load_usesRegisteredFactoryAndReturnsNullForUnknownScene()
    {
        var registry = new SceneRegistry();
        var sceneId = SceneId.New();
        var scene = new Scene(sceneId);

        registry.Register(sceneId, () => scene);

        Assert.Same(scene, registry.Load(sceneId));
        Assert.Null(registry.Load(SceneId.New()));
    }

    /// <summary>
    /// Verifies a scene identifier cannot silently replace its registered factory.
    /// </summary>
    [Fact]
    public void Register_throwsWhenSceneIdentifierIsAlreadyRegistered()
    {
        var registry = new SceneRegistry();
        var sceneId = SceneId.New();
        registry.Register(sceneId, () => new Scene(sceneId));

        Assert.Throws<ArgumentException>(() =>
            registry.Register(sceneId, () => new Scene(sceneId))
        );
    }

    /// <summary>
    /// Verifies a registered factory cannot return a null scene.
    /// </summary>
    [Fact]
    public void Load_throwsWhenRegisteredFactoryReturnsNull()
    {
        var registry = new SceneRegistry();
        var sceneId = SceneId.New();
        registry.Register(sceneId, () => null!);

        Assert.Throws<InvalidOperationException>(() => registry.Load(sceneId));
    }
}
