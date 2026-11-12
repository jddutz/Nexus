namespace Nexus.Input.Events;

/// <summary>Reports an owned touch canceled when the view stops propagating input or is unregistered.</summary>
/// <remarks>If projection is unavailable, the accepted touch-start ray is retained.</remarks>
/// <param name="touchScreen">The touch device that owns the touch.</param>
/// <param name="touchId">The device-local identifier of the canceled touch.</param>
/// <param name="position">The last reported screen-space touch position.</param>
/// <param name="worldRay">The camera ray through the last position, or the accepted start ray.</param>
public sealed class ViewTouchCanceledEvent(
    ITouchScreenInputDevice touchScreen,
    int touchId,
    Vector2D<float> position,
    Ray3D<float> worldRay
) : IEvent
{
    /// <summary>Gets the touch device that owns the touch.</summary>
    public ITouchScreenInputDevice TouchScreen { get; } = touchScreen;

    /// <summary>Gets the device-local identifier of the canceled touch.</summary>
    public int TouchId { get; } = touchId;

    /// <summary>Gets the last reported screen-space touch position.</summary>
    public Vector2D<float> Position { get; } = position;

    /// <summary>Gets the camera ray through the last position, or the accepted start ray.</summary>
    public Ray3D<float> WorldRay { get; } = worldRay;
}
