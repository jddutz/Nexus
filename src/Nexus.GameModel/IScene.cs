namespace Nexus.GameModel;

public interface IScene : IGameObject
{
    Vector4D<float> BackgroundColor { get; }
}
