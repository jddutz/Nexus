namespace Nexus.Graphics.Cameras;

/// <summary>
/// Movable perspective world camera with a configurable field of view, aspect ratio, and clipping planes.
/// </summary>
public class PerspectiveCamera : Component, ICameraComponent
{
    private Vector3D<float> _position = Vector3D<float>.Zero;
    private Vector3D<float> _forward = -Vector3D<float>.UnitZ;
    private Vector3D<float> _up = Vector3D<float>.UnitY;
    private Vector3D<float> _right = Vector3D<float>.UnitX;

    private float _fieldOfView = MathF.PI / 4f;
    private float _nearPlane = 0.1f;
    private float _farPlane = 1000f;
    private float _aspectRatio = 16f / 9f;

    private bool _matricesDirty = true;
    private Matrix4X4<float> _viewMatrix;
    private Matrix4X4<float> _projectionMatrix;
    private Matrix4X4<float> _viewProjectionMatrix;
    private bool _viewProjectionDirty = true;

    /// <summary>Gets the renderable contributions produced by this camera.</summary>
    public IReadOnlyList<IDrawable> Renderables => [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PerspectiveCamera"/> class and computes its initial matrices.
    /// </summary>
    public PerspectiveCamera()
    {
        UpdateDirectionVectors();
        UpdateMatrices();
    }

    /// <summary>Gets the camera's derived right direction, orthogonal to <see cref="Forward"/> and <see cref="Up"/>.</summary>
    public Vector3D<float> Right => _right;

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

    /// <summary>Gets or sets the camera's forward direction. Setting it normalizes the value and re-derives <see cref="Right"/>.</summary>
    public Vector3D<float> Forward
    {
        get => _forward;
        set
        {
            if (!SetProperty(ref _forward, Vector3D.Normalize(value)))
                return;

            UpdateDirectionVectors();
            InvalidateMatrices();
        }
    }

    /// <summary>Gets or sets the camera's up direction. Setting it normalizes the value and re-derives <see cref="Right"/>.</summary>
    public Vector3D<float> Up
    {
        get => _up;
        set
        {
            if (!SetProperty(ref _up, Vector3D.Normalize(value)))
                return;

            UpdateDirectionVectors();
            InvalidateMatrices();
        }
    }

    /// <summary>Gets or sets the vertical field of view, in radians.</summary>
    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            if (!SetProperty(ref _fieldOfView, value))
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

    /// <summary>
    /// Gets the viewport aspect ratio (width divided by height). Established by
    /// <see cref="SetViewportSize"/> since viewport dimensions are the authoritative source.
    /// </summary>
    public float AspectRatio => _aspectRatio;

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

    /// <summary>Gets the perspective projection matrix, recalculating it only when camera state has changed.</summary>
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
    public void SetViewportSize(float width, float height)
    {
        var aspectRatio = width / height;

        if (_aspectRatio == aspectRatio)
            return;

        _aspectRatio = aspectRatio;
        InvalidateMatrices();
    }

    /// <summary>Re-derives <see cref="Right"/> (and re-orthogonalizes <see cref="Up"/>) from the current forward/up vectors.</summary>
    private void UpdateDirectionVectors()
    {
        _right = Vector3D.Normalize(Vector3D.Cross(_forward, _up));
        _up = Vector3D.Normalize(Vector3D.Cross(_right, _forward));
    }

    /// <summary>Marks the view and projection matrices dirty so they are recalculated on next access.</summary>
    private void InvalidateMatrices() => _matricesDirty = true;

    /// <summary>Recalculates the view and projection matrices from the current camera state.</summary>
    private void UpdateMatrices()
    {
        var target = _position + _forward;
        _viewMatrix = Matrix4X4.CreateLookAt(_position, target, _up);
        _projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView(
            _fieldOfView,
            _aspectRatio,
            _nearPlane,
            _farPlane
        );

        _matricesDirty = false;
        _viewProjectionDirty = true;
    }

    /// <inheritdoc/>
    public bool IsVisible(Box3D<float> bounds)
    {
        var center = (bounds.Min + bounds.Max) * 0.5f;
        var distance = Vector3D.Distance(_position, center);

        if (distance < _nearPlane || distance > _farPlane)
            return false;

        var directionToCenter = Vector3D.Normalize(center - _position);
        var dot = Vector3D.Dot(directionToCenter, _forward);

        return dot > 0;
    }

    /// <inheritdoc/>
    public Ray3D<float> ScreenToWorldRay(
        Vector2D<int> screenPoint,
        int screenWidth,
        int screenHeight
    )
    {
        var x = (2.0f * screenPoint.X) / screenWidth - 1.0f;
        var y = 1.0f - (2.0f * screenPoint.Y) / screenHeight;

        var rayClip = new Vector4D<float>(x, y, -1.0f, 1.0f);

        Matrix4X4.Invert(ProjectionMatrix, out var invProjection);
        var rayEye = Vector4D.Transform(rayClip, invProjection);
        rayEye = new Vector4D<float>(rayEye.X, rayEye.Y, -1.0f, 0.0f);

        Matrix4X4.Invert(ViewMatrix, out var invView);
        var rayWorld = Vector4D.Transform(rayEye, invView);
        var rayDirection = Vector3D.Normalize(
            new Vector3D<float>(rayWorld.X, rayWorld.Y, rayWorld.Z)
        );

        return new Ray3D<float>(_position, rayDirection);
    }

    /// <inheritdoc/>
    public Vector2D<int> WorldToScreenPoint(
        Vector3D<float> worldPoint,
        int screenWidth,
        int screenHeight
    )
    {
        var clipSpace = Vector4D.Transform(
            new Vector4D<float>(worldPoint, 1.0f),
            ViewMatrix * ProjectionMatrix
        );

        if (MathF.Abs(clipSpace.W) < float.Epsilon)
            return new Vector2D<int>(-1, -1);

        var ndc = new Vector3D<float>(
            clipSpace.X / clipSpace.W,
            clipSpace.Y / clipSpace.W,
            clipSpace.Z / clipSpace.W
        );

        var screenX = (int)((ndc.X + 1.0f) * 0.5f * screenWidth);
        var screenY = (int)((1.0f - ndc.Y) * 0.5f * screenHeight);

        return new Vector2D<int>(screenX, screenY);
    }

    /// <summary>Moves the camera by the specified translation.</summary>
    /// <param name="translation">The offset to apply to the camera position.</param>
    public void Translate(Vector3D<float> translation) => Position += translation;

    /// <summary>Orients the camera to face the specified world-space target.</summary>
    /// <param name="target">The point to look at.</param>
    public void LookAt(Vector3D<float> target) => Forward = Vector3D.Normalize(target - Position);
}
