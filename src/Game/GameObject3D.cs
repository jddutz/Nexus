namespace Nexus.Game;

/// <summary>
/// Provides a three-dimensional game object.
/// </summary>
public class GameObject3D : GameObject, IGameObject3D
{
    private Vector3D<float> _position = Vector3D<float>.Zero;
    private Quaternion<float> _rotation = Quaternion<float>.Identity;
    private Vector3D<float> _scale = Vector3D<float>.One;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;

    /// <summary>
    /// Gets the transformation matrix for this game object.
    /// </summary>
    protected Matrix4X4<float> TransformationMatrix => _transformationMatrix;

    /// <summary>
    /// Gets or sets the position of this game object.
    /// </summary>
    public Vector3D<float> Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
                UpdateTransformationMatrix();
        }
    }

    /// <summary>
    /// Gets or sets the rotation of this game object.
    /// </summary>
    public Quaternion<float> Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public Vector3D<float> Scale
    {
        get => _scale;
        set
        {
            if (SetProperty(ref _scale, value))
                UpdateTransformationMatrix();
        }
    }

    /// <inheritdoc/>
    public Quaternion<float> Quaternion
    {
        get => _rotation;
        set => Rotation = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject3D"/> class.
    /// </summary>
    public GameObject3D() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameObject3D"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier for the game object.</param>
    public GameObject3D(uint id)
        : base(id) { }

    /// <summary>
    /// Updates the transformation matrix from the position, rotation, and scale.
    /// </summary>
    private void UpdateTransformationMatrix()
    {
        var scale = Matrix4X4.CreateScale(_scale);
        var rotation = Matrix4X4.CreateFromQuaternion(_rotation);
        var translation = Matrix4X4.CreateTranslation(_position);
        _transformationMatrix = scale * rotation * translation;
        OnPropertyChanged(nameof(TransformationMatrix));
    }
}