namespace Nexus.Core;

/// <summary>
/// Provides the default identity, ownership, observation, and lifecycle behavior for a component.
/// </summary>
public abstract partial class Component : IComponent
{
    [Observable]
    private IGameObject? _owner;

    [Observable(SetterIsProtected = true)]
    private bool _isInitialized;

    [Observable(SetterIsProtected = true)]
    private bool _isActivated;

    /// <inheritdoc />
    public event Action<string>? PropertyChanged;

    /// <inheritdoc />
    public event EventHandler? Modified;

    /// <inheritdoc />
    public virtual string DisplayName => GetType().Name;

    /// <inheritdoc />
    public ComponentId Id { get; } = ComponentId.New();

    /// <inheritdoc />
    public virtual void Initialize()
    {
        if (IsInitialized)
            return;

        SetIsInitialized(true);
        OnInitialize();
    }

    /// <inheritdoc />
    public virtual bool CanActivate() => true;

    /// <inheritdoc />
    public virtual void Activate()
    {
        SetIsActivated(CanActivate());
    }

    /// <inheritdoc />
    public virtual void Update(double deltaTime)
    {
        // Intentionally a no-op.
    }

    /// <inheritdoc />
    public virtual void Deactivate()
    {
        SetIsActivated(false);
    }

    /// <summary>
    /// Performs component-specific initialization after the initialized state changes.
    /// </summary>
    protected virtual void OnInitialize() { }

    /// <summary>
    /// Handles a change to this component's owner.
    /// </summary>
    protected virtual void OnOwnerChanged()
    {
        Modified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles a property change on this component's owner.
    /// </summary>
    /// <param name="propertyName">The name of the changed owner property.</param>
    protected virtual void OnOwnerPropertyChanged(string propertyName)
    {
        Modified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates owner property subscriptions after the owner changes.
    /// </summary>
    /// <param name="previousValue">The previous owner.</param>
    private void AfterOwnerChanges(IGameObject? previousValue)
    {
        if (previousValue is not null)
            previousValue.PropertyChanged -= OnOwnerPropertyChanged;

        if (Owner is not null)
            Owner.PropertyChanged += OnOwnerPropertyChanged;

        OnOwnerChanged();
    }

    /// <summary>
    /// Sets a field and raises a property-change notification when its value changes.
    /// </summary>
    /// <typeparam name="T">The field's value type.</typeparam>
    /// <param name="field">The field to update.</param>
    /// <param name="value">The value to assign.</param>
    /// <param name="propertyName">The name of the associated property.</param>
    /// <returns><see langword="true"/> if the field changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the changed property.</param>
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(propertyName);
    }

    /// <summary>
    /// Raises <see cref="Modified"/> after the initialization state changes.
    /// </summary>
    /// <param name="previousValue">The initialization state before the change.</param>
    private void AfterIsInitializedChanges(bool previousValue)
    {
        Modified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises <see cref="Modified"/> after the activation state changes.
    /// </summary>
    /// <param name="previousValue">The activation state before the change.</param>
    private void AfterIsActivatedChanges(bool previousValue)
    {
        Modified?.Invoke(this, EventArgs.Empty);
    }
}
