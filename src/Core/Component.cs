namespace Nexus.Core;

public abstract class Component : ObservableObject, IComponent
{
    private IGameObject? _gameObject;
    private bool _isActivated;

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the identifier of the game object that owns this component.
    /// </summary>
    public GameObjectId GameObjectId => _gameObject?.Id ?? GameObjectId.Invalid;

    /// <summary>
    /// Gets the game model that owns this component's game object.
    /// </summary>
    public IGameModel? GameModel => _gameObject?.GameModel;

    /// <inheritdoc/>
    public void SetGameObject(IGameObject? gameObject)
    {
        if (ReferenceEquals(_gameObject, gameObject))
            return;

        if (_gameObject is not null)
            _gameObject.PropertyChanged -= OnGameObjectPropertyChanged;

        _gameObject = gameObject;

        if (_gameObject is not null)
            _gameObject.PropertyChanged += OnGameObjectPropertyChanged;
    }

    /// <summary>
    /// Gets the unique identifier for this component.
    /// </summary>
    public ComponentId Id { get; } = ComponentId.New();

    /// <summary>
    /// Gets or sets a value indicating whether this component is activated.
    /// </summary>
    public bool IsActivated
    {
        get => _isActivated;
        set => SetProperty(ref _isActivated, value);
    }

    /// <summary>
    /// Occurs when this component's effective state has changed, whether from one of its own
    /// properties or from a change on its owning game object.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Raises <see cref="Changed"/> in addition to <see cref="ObservableObject.PropertyChanged"/>,
    /// since any change to a component's own property alters its effective state.
    /// </summary>
    /// <param name="propertyName">The name of the changed property, supplied automatically by the compiler.</param>
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        OnChanged();
    }

    /// <summary>
    /// Responds to a property change on the owning game object. The default implementation
    /// conservatively treats any owner change as invalidating this component's effective state.
    /// </summary>
    /// <param name="e">The event data describing which owner property changed.</param>
    protected virtual void OnOwnerPropertyChanged(PropertyChangedEventArgs e) => OnChanged();

    /// <summary>
    /// Raises <see cref="Changed"/>.
    /// </summary>
    protected virtual void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Forwards a property-changed notification from the owning game object.
    /// </summary>
    private void OnGameObjectPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnOwnerPropertyChanged(e);
}
