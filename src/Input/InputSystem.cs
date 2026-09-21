namespace Nexus.Input;

/// <summary>
/// Provides the default input system implementation.
/// </summary>
public sealed class InputSystem(IEventHub eventHub) : IInputSystem
{
    public void Initialize()
    {
        eventHub.Register(this);
    }

    /// <inheritdoc />
    public void Update(double deltaTime) { }

    public bool Activate<TComponent>(TComponent component)
        where TComponent : class, IInputComponent
    {
        return false;
    }

    public bool Deactivate<TComponent>(TComponent component)
        where TComponent : class, IInputComponent
    {
        return false;
    }
}
