namespace Nexus.Core;

public interface ISceneRegistry
{
    IScene? Load(uint sceneId);
}
