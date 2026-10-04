namespace Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;

/// <summary>Verifies how the game system selects its initial scene.</summary>
public class GameSystemStartSceneTests
{
    /// <summary>Verifies a configured start scene is selected from multiple registered scenes.</summary>
    [Fact]
    public void Initialize_loadsConfiguredSceneWhenMultipleScenesAreRegistered()
    {
        var registry = SceneRegistryTestHelper.CreateEmptyRegistry();
        var firstScene = CreateScene();
        var configuredScene = CreateScene();
        registry.Register("First", () => firstScene);
        registry.Register("Configured", () => configuredScene);
        var gameSystem = CreateGameSystem(registry, "Configured");

        gameSystem.Initialize();

        Assert.Same(configuredScene, gameSystem.CurrentScene);
    }

    /// <summary>Verifies a configured scene that is not registered fails clearly.</summary>
    [Fact]
    public void Initialize_throwsWhenConfiguredSceneIsMissing()
    {
        var gameSystem = CreateGameSystem(SceneRegistryTestHelper.CreateEmptyRegistry(), "Missing");

        var exception = Assert.Throws<InvalidOperationException>(gameSystem.Initialize);

        Assert.Contains("Start scene 'Missing' is not registered.", exception.Message);
    }

    /// <summary>Verifies the only registered scene is selected without configuration.</summary>
    [Fact]
    public void Initialize_usesOnlyRegisteredSceneWhenStartSceneIsUnspecified()
    {
        var registry = SceneRegistryTestHelper.CreateEmptyRegistry();
        var scene = CreateScene();
        registry.Register("OnlyScene", () => scene);
        var gameSystem = CreateGameSystem(registry);

        gameSystem.Initialize();

        Assert.Same(scene, gameSystem.CurrentScene);
    }

    /// <summary>Verifies an empty registry produces an explicit discovery error.</summary>
    [Fact]
    public void Initialize_throwsWhenNoSceneIsRegistered()
    {
        var gameSystem = CreateGameSystem(SceneRegistryTestHelper.CreateEmptyRegistry());

        var exception = Assert.Throws<InvalidOperationException>(gameSystem.Initialize);

        Assert.Contains("No scene was discovered.", exception.Message);
    }

    /// <summary>Verifies multiple scenes require configuration and are listed in the error.</summary>
    [Fact]
    public void Initialize_throwsAndListsAvailableScenesWhenSeveralAreRegistered()
    {
        var registry = SceneRegistryTestHelper.CreateEmptyRegistry();
        registry.Register("SceneA", CreateScene);
        registry.Register("SceneB", CreateScene);
        var gameSystem = CreateGameSystem(registry);

        var exception = Assert.Throws<InvalidOperationException>(gameSystem.Initialize);

        Assert.Contains("Game:StartSceneId", exception.Message);
        Assert.Contains("SceneA", exception.Message);
        Assert.Contains("SceneB", exception.Message);
    }

    /// <summary>Verifies a selected scene's construction failure is not hidden.</summary>
    [Fact]
    public void Initialize_propagatesSelectedSceneConstructionFailure()
    {
        var expectedException = new InvalidOperationException("Scene construction failed.");
        var registry = SceneRegistryTestHelper.CreateEmptyRegistry();
        registry.Register("Broken", () => throw expectedException);
        registry.Register("Fallback", CreateScene);
        var gameSystem = CreateGameSystem(registry, "Broken");

        var exception = Assert.Throws<InvalidOperationException>(gameSystem.Initialize);

        Assert.Same(expectedException, exception);
    }

    /// <summary>Creates a GameSystem with the supplied registry and optional start scene.</summary>
    /// <param name="registry">The registry used to discover and load scenes.</param>
    /// <param name="startSceneId">The optional configured scene identifier.</param>
    /// <returns>A game system ready to initialize.</returns>
    private static GameSystem CreateGameSystem(SceneRegistry registry, string? startSceneId = null) =>
        new(
            new EventHub(),
            NullLogger<GameSystem>.Instance,
            registry,
            Options.Create(new GameSettings { StartSceneId = startSceneId })
        );

    /// <summary>Creates a scene with the required main camera.</summary>
    /// <returns>A new scene.</returns>
    private static Scene CreateScene() =>
        new()
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
}
