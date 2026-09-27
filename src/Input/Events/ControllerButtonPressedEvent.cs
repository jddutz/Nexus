namespace Nexus.Input.Events;

/// <summary>Indicates that a controller button transitioned to pressed.</summary>
/// <param name="controller">The controller that reported the transition.</param>
/// <param name="button">The button that transitioned.</param>
public sealed class ControllerButtonPressedEvent(IController controller, IButtonInput button)
    : IEvent
{
    /// <summary>Gets the originating controller.</summary>
    public IController Controller { get; } = controller;

    /// <summary>Gets the controller-local logical button index.</summary>
    public int ButtonIndex { get; } = button.Index;

    /// <summary>Gets the pressed state captured when this event was created.</summary>
    public bool IsPressed { get; } = true;

    /// <summary>Gets the transitioned button.</summary>
    public IButtonInput Button { get; } = button;
}
