namespace Nexus.Core;

/// <summary>
/// Defines the lifecycle state and operations of an entity managed by a system.
/// </summary>
public interface IManagedEntity
{
    /// <summary>
    /// Gets a value indicating whether this entity is initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes this entity for use by its managing system.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Gets a value indicating whether this entity is currently activated.
    /// </summary>
    bool IsActivated { get; }

    /// <summary>
    /// Determines whether this entity can be activated in its current state.
    /// </summary>
    /// <returns><see langword="true"/> if this entity can be activated; otherwise, <see langword="false"/>.</returns>
    bool CanActivate();

    /// <summary>
    /// Activates this entity.
    /// </summary>
    void Activate();

    /// <summary>
    /// Updates this entity using the elapsed time since its previous update.
    /// </summary>
    /// <param name="deltaTime">The elapsed time in seconds since the previous update.</param>
    void Update(double deltaTime);

    /// <summary>
    /// Deactivates this entity from its managing system.
    /// </summary>
    void Deactivate();
}
