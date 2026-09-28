namespace Nexus.GUI.Elements;

/// <summary>
/// Provides pointer identity, position, and mouse-button context for GUI pointer actions.
/// </summary>
public class PointerEventArgs : EventArgs
{
    /// <summary>Gets the stable identifier of the pointer.</summary>
    public InputDeviceId PointerId { get; }

    /// <summary>Gets the pointer position in window coordinates.</summary>
    public Vector2D<float> Position { get; }

    /// <summary>Gets the mouse button associated with the event, or <see cref="MouseButtonEnum.Unknown"/>.</summary>
    public MouseButtonEnum Button { get; }

    /// <summary>Initializes pointer event data.</summary>
    /// <param name="pointerId">The stable pointer identifier.</param>
    /// <param name="position">The pointer position in window coordinates.</param>
    /// <param name="button">The mouse button associated with the event.</param>
    public PointerEventArgs(
        InputDeviceId pointerId,
        Vector2D<float> position,
        MouseButtonEnum button = MouseButtonEnum.Unknown
    )
    {
        PointerId = pointerId;
        Position = position;
        Button = button;
    }
}

/// <summary>Provides pointer data for a normally released pointer press.</summary>
public sealed class PointerReleasedEventArgs : PointerEventArgs
{
    /// <summary>Gets whether the release position lies within the element's current bounds.</summary>
    public bool IsInside { get; }

    /// <summary>Initializes pointer-release event data.</summary>
    /// <param name="pointerId">The stable pointer identifier.</param>
    /// <param name="position">The pointer position in window coordinates.</param>
    /// <param name="isInside">Whether the release position is inside the target bounds.</param>
    /// <param name="button">The mouse button associated with the release.</param>
    public PointerReleasedEventArgs(
        InputDeviceId pointerId,
        Vector2D<float> position,
        bool isInside,
        MouseButtonEnum button = MouseButtonEnum.Unknown
    )
        : base(pointerId, position, button) => IsInside = isInside;
}
