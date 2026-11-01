namespace Nexus.Input.Events;

using Nexus.Input.Devices;

/// <summary>Indicates that a touch point has started.</summary>
public sealed class TouchStartedEvent(
    ITouchScreenInputDevice touchScreen,
    int touchId,
    Vector2D<float> position
) : IEvent
{
    public ITouchScreenInputDevice TouchScreen { get; } = touchScreen;
    public int TouchId { get; } = touchId;
    public Vector2D<float> Position { get; } = position;
}

/// <summary>Indicates that a touch point has moved.</summary>
public sealed class TouchMovedEvent(
    ITouchScreenInputDevice touchScreen,
    int touchId,
    Vector2D<float> position
) : IEvent
{
    public ITouchScreenInputDevice TouchScreen { get; } = touchScreen;
    public int TouchId { get; } = touchId;
    public Vector2D<float> Position { get; } = position;
}

/// <summary>Indicates that a touch point has ended.</summary>
public sealed class TouchEndedEvent(
    ITouchScreenInputDevice touchScreen,
    int touchId,
    Vector2D<float> position
) : IEvent
{
    public ITouchScreenInputDevice TouchScreen { get; } = touchScreen;
    public int TouchId { get; } = touchId;
    public Vector2D<float> Position { get; } = position;
}
