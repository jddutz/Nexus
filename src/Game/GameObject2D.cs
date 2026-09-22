namespace Nexus.Game;

/// <summary>
/// Provides a two-dimensional game object.
/// </summary>
public class GameObject2D : GameObject, IGameObject2D
{
    private Vector2D<float> _position = Vector2D<float>.Zero;
    private float _rotation;
    private Vector2D<float> _scale = Vector2D<float>.One;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;

    /// <summary>
    /// Gets the transformation matrix for this game object.
    /// </summary>
    protected Matrix4X4<float> TransformationMatrix => _transformationMatrix;

    /// <inheritdoc/>
    public Vector2D<float> Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public float Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public Vector2D<float> Scale
    {
        get => _scale;
        set
        {
            if (SetProperty(ref _scale, value))
                UpdateTransformationMatrix();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject2D"/> class.
    /// </summary>
    public GameObject2D() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject2D"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the game object.</param>
    public GameObject2D(uint id)
        : base(id) { }

    /// <summary>
    /// Updates the transformation matrix from the position, rotation, and scale.
    /// </summary>
    private void UpdateTransformationMatrix()
    {
        var scale = Matrix4X4.CreateScale(_scale.X, _scale.Y, 1.0f);
        var rotation = Matrix4X4.CreateRotationZ(_rotation);
        var translation = Matrix4X4.CreateTranslation(_position.X, _position.Y, 0.0f);
        _transformationMatrix = scale * rotation * translation;
        OnPropertyChanged(nameof(TransformationMatrix));
    }
}