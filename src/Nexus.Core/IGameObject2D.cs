namespace Nexus.Core;

public interface IGameObject2D : IGameObject
{
    Vector2D<float> Position { get; set; }

    float Rotation { get; set; }

    Vector2D<float> Scale { get; set; }
}
