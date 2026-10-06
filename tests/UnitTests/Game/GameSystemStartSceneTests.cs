namespace Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;

/// <summary>Verifies the game system loads and manages its current scene.</summary>
public class GameSystemStartSceneTests
{
    /// <summary>Verifies game-system initialization does not select a start scene.</summary>
    [Fact]
    public void Initialize_doesNotRequireASelectedScene()
    {
        var gameSystem = CreateGameSystem();

        gameSystem.Initialize();

        Assert.Null(gameSystem.CurrentScene);
        Assert.False(gameSystem.IsSceneLoaded);
    }

    /// <summary>Verifies selecting a scene does not mark it loaded.</summary>
    [Fact]
    public void IsSceneLoaded_isFalseForAnUnloadedCurrentScene()
    {
        var scene = CreateScene();
        var gameSystem = new SceneSelectingGameSystem(scene);

        Assert.Same(scene, gameSystem.CurrentScene);
        Assert.False(scene.IsActivated);
        Assert.False(gameSystem.IsSceneLoaded);
    }

    /// <summary>Verifies loaded status reflects the current scene's activation state.</summary>
    [Fact]
    public void IsSceneLoaded_reflectsCurrentSceneActivation()
    {
        var scene = CreateScene();
        var gameSystem = new SceneSelectingGameSystem(scene);

        scene.Initialize();
        Assert.False(gameSystem.IsSceneLoaded);

        scene.Activate();

        Assert.True(scene.IsActivated);
        Assert.True(gameSystem.IsSceneLoaded);
    }

    /// <summary>Verifies loading activates the scene hierarchy before returning.</summary>
    [Fact]
    public void LoadScene_initializesAndActivatesTheHierarchy()
    {
        var scene = CreateScene();
        var child = new SceneGameObject();
        scene.Children.Add(child);
        var gameSystem = CreateGameSystem();

        gameSystem.LoadScene(scene);

        Assert.Same(scene, gameSystem.CurrentScene);
        Assert.True(gameSystem.IsSceneLoaded);
        Assert.True(scene.IsInitialized);
        Assert.True(scene.IsActivated);
        Assert.True(child.IsInitialized);
        Assert.True(child.IsActivated);
    }

    /// <summary>Verifies loading publishes the scene event after activation.</summary>
    [Fact]
    public void LoadScene_publishesSceneLoadedEventAfterActivation()
    {
        var scene = CreateScene();
        var eventHub = new EventHub();
        var observer = new SceneLoadedObserver();
        var gameSystem = new GameSystem(eventHub, NullLogger<GameSystem>.Instance);
        eventHub.Register(observer);

        gameSystem.LoadScene(scene);
        eventHub.Drain();

        Assert.Same(scene, observer.Scene);
        Assert.True(observer.WasActivatedWhenPublished);
    }

    /// <summary>Verifies activation changes raise observable property notifications.</summary>
    [Fact]
    public void Activation_notifiesWhenSceneLoadsAndUnloads()
    {
        var scene = CreateScene();
        var propertyChanges = new List<string>();
        scene.PropertyChanged += propertyChanges.Add;
        var gameSystem = CreateGameSystem();

        gameSystem.LoadScene(scene);
        gameSystem.UnloadScene();

        Assert.Equal(
            2,
            propertyChanges.Count(propertyName => propertyName == nameof(Scene.IsActivated))
        );
    }

    /// <summary>Verifies loading another scene requires explicitly unloading the current one.</summary>
    [Fact]
    public void LoadScene_throwsWhenAnotherSceneIsLoaded()
    {
        var initialScene = CreateScene();
        var gameSystem = CreateGameSystem();
        gameSystem.LoadScene(initialScene);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            gameSystem.LoadScene(CreateScene())
        );

        Assert.Contains("Unload the current scene", exception.Message);
        Assert.Same(initialScene, gameSystem.CurrentScene);
        Assert.True(gameSystem.IsSceneLoaded);
    }

    /// <summary>Verifies unloading deactivates the hierarchy and is harmless when repeated.</summary>
    [Fact]
    public void UnloadScene_deactivatesTheHierarchyAndIsIdempotent()
    {
        var scene = CreateScene();
        var child = new SceneGameObject();
        scene.Children.Add(child);
        var gameSystem = CreateGameSystem();
        gameSystem.LoadScene(scene);

        gameSystem.UnloadScene();
        gameSystem.UnloadScene();

        Assert.False(gameSystem.IsSceneLoaded);
        Assert.Same(scene, gameSystem.CurrentScene);
        Assert.False(scene.IsActivated);
        Assert.False(child.IsActivated);
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

    /// <summary>Provides a managed game object for scene lifecycle tests.</summary>
    private sealed class SceneGameObject : GameObject;

    /// <summary>Provides a selected current scene without loading its hierarchy.</summary>
    private sealed class SceneSelectingGameSystem : GameSystem
    {
        /// <summary>Initializes the helper with a current scene selection.</summary>
        /// <param name="scene">The scene to select without loading.</param>
        public SceneSelectingGameSystem(IScene scene)
            : base(new EventHub(), NullLogger<GameSystem>.Instance)
        {
            CurrentScene = scene;
        }
    }

    /// <summary>Records the scene and activation state from the global loaded event.</summary>
    private sealed class SceneLoadedObserver
    {
        /// <summary>Gets the scene received in the loaded event, if one was handled.</summary>
        public IScene? Scene { get; private set; }

        /// <summary>Gets whether the scene was activated when its loaded event was handled.</summary>
        public bool WasActivatedWhenPublished { get; private set; }

        /// <summary>Records the loaded scene and its activation state.</summary>
        /// <param name="message">The published scene-loaded event.</param>
        public void Handle(SceneLoadedEvent message)
        {
            Scene = message.Scene;
            WasActivatedWhenPublished = message.Scene.IsActivated;
        }
    }
}
