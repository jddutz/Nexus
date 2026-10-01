using Nexus.Core;

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
        const string sceneName = "WelcomeScreen";
        var scene = new Scene(NodeId.New());

        registry.Register(sceneName, () => scene);

        Assert.Same(scene, registry.Load(sceneName));
        Assert.Null(registry.Load("UnknownScene"));
    }

    /// <summary>
    /// Verifies a scene name cannot silently replace its registered factory.
    /// </summary>
    [Fact]
    public void Register_throwsWhenSceneNameIsAlreadyRegistered()
    {
        var registry = new SceneRegistry();
        const string sceneName = "DuplicateScene";
        registry.Register(sceneName, () => new Scene(NodeId.New()));

        Assert.Throws<ArgumentException>(() =>
            registry.Register(sceneName, () => new Scene(NodeId.New()))
        );
    }

    /// <summary>
    /// Verifies a registered factory cannot return a null scene.
    /// </summary>
    [Fact]
    public void Load_throwsWhenRegisteredFactoryReturnsNull()
    {
        var registry = new SceneRegistry();
        const string sceneName = "NullScene";
        registry.Register(sceneName, () => null!);

        Assert.Throws<InvalidOperationException>(() => registry.Load(sceneName));
    }
}
