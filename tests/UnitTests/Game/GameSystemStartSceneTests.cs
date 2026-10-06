namespace Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;

/// <summary>Verifies the game system loads and manages its current scene.</summary>
public class GameSystemStartSceneTests
{
    /// <summary>Verifies initialization accepts a scene loaded beforehand.</summary>
    [Fact]
    public void Initialize_usesTheLoadedScene()
    {
        var selectedScene = CreateScene();
        var gameSystem = CreateGameSystem();
        gameSystem.LoadScene(selectedScene);

        gameSystem.Initialize();

        Assert.Same(selectedScene, gameSystem.CurrentScene);
    }

    /// <summary>Verifies initialization fails when no scene has been loaded.</summary>
    [Fact]
    public void Initialize_throwsWhenNoSceneHasBeenLoaded()
    {
        var gameSystem = CreateGameSystem();

        var exception = Assert.Throws<InvalidOperationException>(gameSystem.Initialize);

        Assert.Contains("No current scene is loaded.", exception.Message);
        Assert.Null(gameSystem.CurrentScene);
    }

    /// <summary>Verifies loading a replacement scene unloads the previously active scene.</summary>
    [Fact]
    public void LoadScene_unloadsThePreviousSceneAndLoadsTheReplacement()
    {
        var initialScene = CreateScene();
        var nextScene = CreateScene();
        var gameSystem = CreateGameSystem();
        gameSystem.LoadScene(initialScene);
        gameSystem.Initialize();
        gameSystem.Update(0);

        gameSystem.LoadScene(nextScene);

        Assert.Same(nextScene, gameSystem.CurrentScene);
        Assert.False(initialScene.IsActivated);
        gameSystem.Update(0);
        Assert.True(nextScene.IsActivated);
    }

    /// <summary>Creates a GameSystem with its required services.</summary>
    /// <returns>A game system ready to initialize.</returns>
    private static GameSystem CreateGameSystem() =>
        new(new EventHub(), NullLogger<GameSystem>.Instance);

    /// <summary>Creates a scene with the required main camera.</summary>
    /// <returns>A new scene.</returns>
    private static Scene CreateScene() =>
        new()
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
}
