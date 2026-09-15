namespace Nexus.Core;

public class GameObject2D : GameObject, IGameObject2D
{
    private Vector2D<float> _position = Vector2D<float>.Zero;
    private float _rotation;
    private Vector2D<float> _scale = Vector2D<float>.One;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;

    protected Matrix4X4<float> TransformationMatrix => _transformationMatrix;

    public Vector2D<float> Position
    {
        get => _position;
        set
        {
            if (_position == value)
                return;

            _position = value;
            UpdateTransformationMatrix();
        }
    }

    public float Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value)
                return;

            _rotation = value;
            UpdateTransformationMatrix();
        }
    }

    public Vector2D<float> Scale
    {
        get => _scale;
        set
        {
            if (_scale == value)
                return;

            _scale = value;
            UpdateTransformationMatrix();
        }
    }

    public GameObject2D()
        : base() { }

    public GameObject2D(uint id)
        : base(id) { }

    private void UpdateTransformationMatrix()
    {
        var scale = Matrix4X4.CreateScale(_scale.X, _scale.Y, 1.0f);
        var rotation = Matrix4X4.CreateRotationZ(_rotation);
        var translation = Matrix4X4.CreateTranslation(_position.X, _position.Y, 0.0f);

        _transformationMatrix = scale * rotation * translation;
    }
}
