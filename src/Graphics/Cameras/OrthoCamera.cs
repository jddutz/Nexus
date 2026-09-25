namespace Nexus.Graphics.Cameras;

/// <summary>
/// Movable orthographic world camera with a fixed axis-aligned orientation, used for simulating
/// 2D rendering using 3D world coordinates.
/// </summary>
public class OrthoCamera : Component, ICameraComponent
{
    private float _width = 10f;
    private float _height = 10f;
    private float _nearPlane = -1000f;
    private float _farPlane = 1000f;
    private Vector3D<float> _position = Vector3D<float>.Zero;

    private bool _matricesDirty = true;
    private Matrix4X4<float> _viewMatrix;
    private Matrix4X4<float> _projectionMatrix;
    private Matrix4X4<float> _viewProjectionMatrix;
    private bool _viewProjectionDirty = true;

    /// <inheritdoc />
    public override string DisplayName => "Orthographic Camera";

    /// <summary>Gets the renderable contributions produced by this camera.</summary>
    public IReadOnlyList<IDrawable> Renderables => [];

    /// <summary>
    /// Initializes a new instance of the <see cref="OrthoCamera"/> class and computes its initial matrices.
    /// </summary>
    public OrthoCamera()
    {
        UpdateMatrices();
    }

    /// <summary>Gets the fixed forward direction, always -Z.</summary>
    public Vector3D<float> Forward { get; } = -Vector3D<float>.UnitZ;

    /// <summary>Gets the fixed up direction, always +Y.</summary>
    public Vector3D<float> Up { get; } = Vector3D<float>.UnitY;

    /// <summary>Gets the fixed right direction, always +X.</summary>
    public Vector3D<float> Right { get; } = Vector3D<float>.UnitX;

    /// <summary>Gets or sets the world position of the camera.</summary>
    public Vector3D<float> Position
    {
        get => _position;
        set
        {
            if (!SetProperty(ref _position, value))
                return;

            InvalidateMatrices();
        }
    }

    /// <summary>Gets or sets the width of the orthographic view volume.</summary>
    public float Width
    {
        get => _width;
        set
        {
            if (!SetProperty(ref _width, value))
                return;

            InvalidateMatrices();
        }
    }

    /// <summary>Gets or sets the height of the orthographic view volume.</summary>
    public float Height
    {
        get => _height;
        set
        {
            if (!SetProperty(ref _height, value))
                return;

            InvalidateMatrices();
        }
    }

    /// <summary>Gets or sets the near clipping plane distance.</summary>
    public float NearPlane
    {
        get => _nearPlane;
        set
        {
            if (!SetProperty(ref _nearPlane, value))
                return;

            InvalidateMatrices();
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

            InvalidateMatrices();
        }
    }

    /// <summary>Gets the view matrix, recalculating it only when camera state has changed.</summary>
    public Matrix4X4<float> ViewMatrix
    {
        get
        {
            if (_matricesDirty)
                UpdateMatrices();

            return _viewMatrix;
        }
    }

    /// <summary>Gets the orthographic projection matrix, recalculating it only when camera state has changed.</summary>
    public Matrix4X4<float> ProjectionMatrix
    {
        get
        {
            if (_matricesDirty)
                UpdateMatrices();

            return _projectionMatrix;
        }
    }

    /// <inheritdoc/>
    public Matrix4X4<float> ViewProjectionMatrix
    {
        get
        {
            if (_matricesDirty)
                UpdateMatrices();

            if (_viewProjectionDirty)
            {
                _viewProjectionMatrix = _viewMatrix * _projectionMatrix;
                _viewProjectionDirty = false;
            }

            return _viewProjectionMatrix;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The orthographic view volume is defined by <see cref="Width"/> and <see cref="Height"/>
    /// rather than the physical viewport, so this is a no-op unless aspect-preserving behavior
    /// is introduced later.
    /// </remarks>
    public void SetViewportSize(float width, float height) { }

    /// <summary>Marks the view and projection matrices dirty so they are recalculated on next access.</summary>
    private void InvalidateMatrices() => _matricesDirty = true;

    /// <summary>Recalculates the view and projection matrices from the current camera state.</summary>
    private void UpdateMatrices()
    {
        var target = _position + Forward;
        _viewMatrix = Matrix4X4.CreateLookAt(_position, target, Up);
        _projectionMatrix = Matrix4X4.CreateOrthographic(_width, _height, _nearPlane, _farPlane);

        _matricesDirty = false;
        _viewProjectionDirty = true;
    }

    /// <inheritdoc/>
    public bool IsVisible(Box3D<float> bounds)
    {
        var center = (bounds.Min + bounds.Max) * 0.5f;
        var size = bounds.Max - bounds.Min;

        var relativeToCamera = center - _position;

        var halfWidth = _width * 0.5f;
        var halfHeight = _height * 0.5f;

        var rightProjection = Vector3D.Dot(relativeToCamera, Right);
        var upProjection = Vector3D.Dot(relativeToCamera, Up);
        var forwardProjection = Vector3D.Dot(relativeToCamera, Forward);

        var rightExtent = Vector3D.Dot(size, Right) * 0.5f;
        var upExtent = Vector3D.Dot(size, Up) * 0.5f;
        var forwardExtent = Vector3D.Dot(size, Forward) * 0.5f;

        return MathF.Abs(rightProjection) - rightExtent <= halfWidth
            && MathF.Abs(upProjection) - upExtent <= halfHeight
            && forwardProjection - forwardExtent >= _nearPlane
            && forwardProjection + forwardExtent <= _farPlane;
    }

    /// <inheritdoc/>
    public Ray3D<float> ScreenToWorldRay(
        Vector2D<int> screenPoint,
        int screenWidth,
        int screenHeight
    )
    {
        var normalizedX = (2.0f * screenPoint.X) / screenWidth - 1.0f;
        var normalizedY = 1.0f - (2.0f * screenPoint.Y) / screenHeight;

        var worldX = normalizedX * _width * 0.5f;
        var worldY = normalizedY * _height * 0.5f;

        var rayOrigin = _position + (Right * worldX) + (Up * worldY);

        return new Ray3D<float>(rayOrigin, Forward);
    }

    /// <inheritdoc/>
    public Vector2D<int> WorldToScreenPoint(
        Vector3D<float> worldPoint,
        int screenWidth,
        int screenHeight
    )
    {
        var relativeToCamera = worldPoint - _position;

        var rightProjection = Vector3D.Dot(relativeToCamera, Right);
        var upProjection = Vector3D.Dot(relativeToCamera, Up);

        var normalizedX = rightProjection / (_width * 0.5f);
        var normalizedY = upProjection / (_height * 0.5f);

        var screenX = (int)((normalizedX + 1.0f) * 0.5f * screenWidth);
        var screenY = (int)((1.0f - normalizedY) * 0.5f * screenHeight);

        return new Vector2D<int>(screenX, screenY);
    }

    /// <summary>Moves the camera by the specified translation.</summary>
    /// <param name="translation">The offset to apply to the camera position.</param>
    public void Translate(Vector3D<float> translation) => Position += translation;

    /// <summary>Centers the camera's orthographic view on the specified world-space target.</summary>
    /// <param name="target">The point to center the view on.</param>
    /// <remarks>The camera's orientation is fixed; only its X/Y position changes.</remarks>
    public void LookAt(Vector3D<float> target) =>
        Position = new Vector3D<float>(target.X, target.Y, Position.Z);

    /// <summary>Sets the width and height of the orthographic view volume.</summary>
    /// <param name="width">The new view volume width.</param>
    /// <param name="height">The new view volume height.</param>
    public void SetSize(float width, float height)
    {
        Width = width;
        Height = height;
    }
}
