namespace Nexus.GameModel;

public class Scene : IScene
{
    public SceneId Id { get; init; }

    public Composition GetComposition() =>
        new() { Background = new SolidColorBackgroundResource(Colors.CornflowerBlue) };

    public Vector4D<float> BackgroundColor { get; set; }

    public void Update(double deltaTime) { }
}
