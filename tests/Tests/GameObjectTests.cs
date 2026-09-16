using Nexus.Core;

namespace Tests;

/// <summary>
/// Tests game object component ownership and game-model lookup behavior.
/// </summary>
public class GameObjectTests
{
    /// <summary>
    /// Verifies that components can resolve their owner through the assigned game model.
    /// </summary>
    [Fact]
    public void AddComponent_assignsOwnerAndGameModel()
    {
        var gameModel = new TestGameModel();
        var gameObject = new GameObject(1);
        var component = new TestComponent();

        gameObject.SetGameModel(gameModel);
        gameObject.AddComponent(component);

        var componentGameModel = Assert.IsAssignableFrom<IGameModel>(component.GameModel);

        Assert.Equal(gameObject.Id, component.GameObjectId);
        Assert.Same(gameModel, componentGameModel);
        Assert.Same(gameObject, componentGameModel.GetGameObject(component.GameObjectId));
    }

    /// <summary>
    /// Verifies that attaching a component to a new game object detaches it from its prior owner.
    /// </summary>
    [Fact]
    public void AddComponent_transfersComponentFromPreviousOwner()
    {
        var gameModel = new TestGameModel();
        var previousOwner = new GameObject(1);
        var nextOwner = new GameObject(2);
        var component = new TestComponent();

        previousOwner.SetGameModel(gameModel);
        nextOwner.SetGameModel(gameModel);
        previousOwner.AddComponent(component);

        nextOwner.AddComponent(component);

        Assert.Empty(previousOwner.Components);
        Assert.Contains(component, nextOwner.Components);
        Assert.Equal(nextOwner.Id, component.GameObjectId);
        Assert.Same(nextOwner, component.GameModel?.GetGameObject(component.GameObjectId));
    }

    /// <summary>
    /// Provides an in-memory game model for component ownership tests.
    /// </summary>
    private sealed class TestGameModel : IGameModel
    {
        private readonly Dictionary<GameObjectId, IGameObject> _gameObjects = [];

        /// <inheritdoc/>
        public IGameObject? GetGameObject(GameObjectId gameObjectId) =>
            _gameObjects.GetValueOrDefault(gameObjectId);

        /// <inheritdoc/>
        public void RegisterGameObject(IGameObject gameObject) =>
            _gameObjects[gameObject.Id] = gameObject;

        /// <inheritdoc/>
        public void UnregisterGameObject(IGameObject gameObject) =>
            _gameObjects.Remove(gameObject.Id);
    }

    /// <summary>
    /// Provides a concrete component for ownership tests.
    /// </summary>
    private sealed class TestComponent : Component { }
}
