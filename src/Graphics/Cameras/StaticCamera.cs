namespace Nexus.Graphics.Cameras;

/// <summary>
/// Orthographic screen-space camera for UI and 2D rendering, using a top-left origin coordinate
/// system (+X right, +Y down) that maps pixel coordinates directly to Vulkan clip space.
/// </summary>
public class StaticCamera : Component, ICameraComponent
{
    private float _viewportWidth = 1f;
    private float _viewportHeight = 1f;
    private float _nearPlane = -1f;
    private float _farPlane = 1f;

    private Matrix4X4<float> _projectionMatrix;
    private Matrix4X4<float> _viewProjectionMatrix;
    private bool _viewProjectionDirty = true;

    private Rectangle<float> _visibleRect;
    private bool _visibleRectDirty = true;

    /// <summary>Gets the renderable contributions produced by this camera.</summary>
    public IReadOnlyList<IDrawable> Renderables => [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticCamera"/> class with a default 1x1
    /// viewport. Call <see cref="SetViewportSize"/> before rendering.
    /// </summary>
    public StaticCamera()
    {
        InvalidateProjection();
    }

    /// <summary>Gets the camera position, fixed at the origin for screen-space rendering.</summary>
    public Vector3D<float> Position { get; } = Vector3D<float>.Zero;

    /// <summary>Gets the fixed forward direction, always -Z.</summary>
    public Vector3D<float> Forward { get; } = -Vector3D<float>.UnitZ;

    /// <summary>Gets the fixed up direction, always +Y (matching the top-left, Y-down screen convention).</summary>
    public Vector3D<float> Up { get; } = Vector3D<float>.UnitY;

    /// <summary>Gets the fixed right direction, always +X.</summary>
    public Vector3D<float> Right { get; } = Vector3D<float>.UnitX;

    /// <summary>Gets or sets the near clipping plane distance.</summary>
    public float NearPlane
    {
        get => _nearPlane;
        set
        {
            if (!SetProperty(ref _nearPlane, value))
                return;

            InvalidateProjection();
        }
    }

    /// <summary>Gets or sets the far clipping plane distance.</summary>
    public float FarPlane
    {
        get => _farPlane;
        set
        {
            if (!SetProperty(ref _farPlane, value))
                return;

            InvalidateProjection();
        }
    }

    /// <summary>Gets the identity view matrix used for screen-space rendering.</summary>
    public Matrix4X4<float> ViewMatrix { get; } = Matrix4X4<float>.Identity;

    /// <summary>Gets the pixel-to-NDC orthographic projection matrix for the current viewport size.</summary>
    public Matrix4X4<float> ProjectionMatrix => _projectionMatrix;

    /// <inheritdoc/>
    public Matrix4X4<float> ViewProjectionMatrix
    {
        get
        {
            if (_viewProjectionDirty)
            {
                _viewProjectionMatrix = ViewMatrix * _projectionMatrix;
                _viewProjectionDirty = false;
            }

            return _viewProjectionMatrix;
        }
    }

    /// <summary>
    /// Updates the camera's projection to match the viewport dimensions, rebuilding it only when
    /// the dimensions actually change.
    /// </summary>
    /// <param name="width">The viewport width, in pixels.</param>
    /// <param name="height">The viewport height, in pixels.</param>
    public void SetViewportSize(float width, float height)
    {
        if (
            MathF.Abs(_viewportWidth - width) < 0.01f
            && MathF.Abs(_viewportHeight - height) < 0.01f
        )
            return;

        _viewportWidth = width;
        _viewportHeight = height;
        InvalidateProjection();
    }

    /// <summary>Rebuilds the pixel-to-NDC projection matrix and marks dependent caches dirty.</summary>
    private void InvalidateProjection()
    {
        // Top-left origin with +Y down matches Vulkan NDC's downward Y axis directly.
        _projectionMatrix = Matrix4X4.CreateOrthographicOffCenter(
            0f,
            _viewportWidth,
            0f,
            _viewportHeight,
            _nearPlane,
            _farPlane
        );

        _viewProjectionDirty = true;
        _visibleRectDirty = true;
    }

    /// <summary>Recalculates the cached visible rectangle when the viewport size has changed.</summary>
    private void UpdateVisibleRect()
    {
        if (!_visibleRectDirty)
            return;

        _visibleRect = new Rectangle<float>(0f, 0f, _viewportWidth, _viewportHeight);
        _visibleRectDirty = false;
    }

    /// <inheritdoc/>
    public bool IsVisible(Box3D<float> bounds)
    {
        UpdateVisibleRect();

        var overlapX = bounds.Max.X >= _visibleRect.Origin.X && bounds.Min.X <= _visibleRect.Max.X;
        var overlapY = bounds.Max.Y >= _visibleRect.Origin.Y && bounds.Min.Y <= _visibleRect.Max.Y;

        return overlapX && overlapY;
    }

    /// <inheritdoc/>
    public Ray3D<float> ScreenToWorldRay(
        Vector2D<int> screenPoint,
        int screenWidth,
        int screenHeight
    )
    {
        // Top-left origin means screen coordinates equal world coordinates directly.
        var rayOrigin = new Vector3D<float>(screenPoint.X, screenPoint.Y, 0f);
        return new Ray3D<float>(rayOrigin, Forward);
    }

    /// <inheritdoc/>
    public Vector2D<int> WorldToScreenPoint(
        Vector3D<float> worldPoint,
        int screenWidth,
        int screenHeight
    )
    {
        return new Vector2D<int>((int)worldPoint.X, (int)worldPoint.Y);
    }
}
