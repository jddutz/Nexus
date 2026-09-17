namespace Nexus.Core;

public class GameObject3D : GameObject, IGameObject3D
{
    private Vector3D<float> _position = Vector3D<float>.Zero;
    private Quaternion<float> _rotation = Quaternion<float>.Identity;
    private Vector3D<float> _scale = Vector3D<float>.One;
    private Matrix4X4<float> _transformationMatrix = Matrix4X4<float>.Identity;

    protected Matrix4X4<float> TransformationMatrix => _transformationMatrix;

    public Vector3D<float> Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
                UpdateTransformationMatrix();
        }
    }

    public Quaternion<float> Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, value))
                UpdateTransformationMatrix();
        }
    }

    public Vector3D<float> Scale
    {
        get => _scale;
        set
        {
            if (SetProperty(ref _scale, value))
                UpdateTransformationMatrix();
        }
    }

    public Quaternion<float> Quaternion
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public GameObject3D()
        : base() { }

    public GameObject3D(uint id)
        : base(id) { }

    private void UpdateTransformationMatrix()
    {
        var scale = Matrix4X4.CreateScale(_scale);
        var rotation = Matrix4X4.CreateFromQuaternion(_rotation);
        var translation = Matrix4X4.CreateTranslation(_position);

        _transformationMatrix = scale * rotation * translation;

        OnPropertyChanged(nameof(TransformationMatrix));
    }
}
