namespace Nexus.Input.Events;

/// <summary>Reports an owned touch leaving a view's interactive region without ending its interaction.</summary>
/// <param name="touchScreen">The touch device that owns the touch.</param>
/// <param name="touchId">The device-local identifier of the active touch.</param>
/// <param name="position">The touch position or last known position when the region became unavailable.</param>
public sealed class ViewTouchExitedEvent(
    ITouchScreenInputDevice touchScreen,
    int touchId,
    Vector2D<float> position
) : IEvent
{
    /// <summary>Gets the touch device that owns the touch.</summary>
    public ITouchScreenInputDevice TouchScreen { get; } = touchScreen;

    /// <summary>Gets the device-local identifier of the active touch.</summary>
    public int TouchId { get; } = touchId;

    /// <summary>Gets the touch position associated with the exit.</summary>
    public Vector2D<float> Position { get; } = position;
}
