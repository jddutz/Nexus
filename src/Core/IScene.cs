namespace Nexus.Core;

/// <summary>
/// Defines a scene that is the root of a scene-node hierarchy.
/// </summary>
public interface IScene : ISceneNode, IManagedEntity, IObservable
{
    bool IsLoaded { get; set; }
}
