namespace Nexus.Input.Events;

/// <summary>Indicates that a controller analog input changed position.</summary>
/// <param name="controller">The controller that reported the change.</param>
/// <param name="analogInput">The analog input that changed.</param>
/// <param name="position">The normalized position captured at the change.</param>
public sealed class ControllerAnalogChangedEvent(
    IController controller,
    IAnalogInput analogInput,
    Vector2D<float> position
) : IEvent
{
    /// <summary>Gets the originating controller.</summary>
    public IController Controller { get; } = controller;

    /// <summary>Gets the controller-local logical analog-input index.</summary>
    public int AnalogInputIndex { get; } = analogInput.Index;

    /// <summary>Gets the changed analog input.</summary>
    public IAnalogInput AnalogInput { get; } = analogInput;

    /// <summary>Gets the normalized position captured when the event was created.</summary>
    public Vector2D<float> Position { get; } = position;
}
