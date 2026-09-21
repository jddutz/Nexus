namespace Nexus.Graphics.Cameras;

/// <summary>
/// Defines the common contract shared by all camera components. Cameras are activated by the
/// graphics system but contribute no render items; their responsibility ends at exposing camera
/// state, particularly <see cref="ViewProjectionMatrix"/>.
/// </summary>
public interface ICameraComponent
{
    /// <summary>
    /// Gets the view matrix representing the camera's transformation in world space.
    /// </summary>
    Matrix4X4<float> ViewMatrix { get; }

    /// <summary>
    /// Gets the projection matrix used to project view-space coordinates to clip space.
    /// </summary>
    Matrix4X4<float> ProjectionMatrix { get; }

    /// <summary>
    /// Gets the cached combined view-projection matrix (<see cref="ViewMatrix"/> * <see cref="ProjectionMatrix"/>).
    /// </summary>
    Matrix4X4<float> ViewProjectionMatrix { get; }

    /// <summary>
    /// Gets the camera's position in world space.
    /// </summary>
    Vector3D<float> Position { get; }

    /// <summary>
    /// Gets the forward direction vector of the camera in world space.
    /// </summary>
    Vector3D<float> Forward { get; }

    /// <summary>
    /// Gets the up direction vector of the camera in world space.
    /// </summary>
    Vector3D<float> Up { get; }

    /// <summary>
    /// Gets the right direction vector of the camera in world space.
    /// </summary>
    Vector3D<float> Right { get; }

    /// <summary>
    /// Notifies the camera of the actual render-target dimensions, letting it update whatever
    /// projection state derives from viewport size (e.g. pixel-space extents or aspect ratio).
    /// </summary>
    /// <param name="width">The viewport width, in pixels.</param>
    /// <param name="height">The viewport height, in pixels.</param>
    void SetViewportSize(float width, float height);

    /// <summary>
    /// Determines whether the specified 3D bounding box is visible within the camera's view.
    /// </summary>
    /// <param name="bounds">The 3D bounding box to test.</param>
    /// <returns><see langword="true"/> when the bounds are visible; otherwise, <see langword="false"/>.</returns>
    bool IsVisible(Box3D<float> bounds);

    /// <summary>
    /// Converts a screen-space point to a world-space ray for picking or selection.
    /// </summary>
    /// <param name="screenPoint">The screen-space point, in pixel coordinates.</param>
    /// <param name="screenWidth">The width of the screen in pixels.</param>
    /// <param name="screenHeight">The height of the screen in pixels.</param>
    /// <returns>A ray in world space originating from the camera through the screen point.</returns>
    Ray3D<float> ScreenToWorldRay(Vector2D<int> screenPoint, int screenWidth, int screenHeight);

    /// <summary>
    /// Projects a world-space point to screen-space coordinates.
    /// </summary>
    /// <param name="worldPoint">The world-space point to project.</param>
    /// <param name="screenWidth">The width of the screen in pixels.</param>
    /// <param name="screenHeight">The height of the screen in pixels.</param>
    /// <returns>The screen-space coordinates (pixel position) of the world point.</returns>
    Vector2D<int> WorldToScreenPoint(Vector3D<float> worldPoint, int screenWidth, int screenHeight);
}
