namespace Nexus.Core;

/// <summary>
/// Defines a scene that is the root of a scene-node hierarchy.
/// </summary>
public interface IScene : ISceneNode, IManagedEntity, IObservable
{
    /// <summary>Gets or sets the camera used by the GUI-owned full-screen scene view.</summary>
    ICamera MainCamera { get; set; }

    bool IsLoaded { get; set; }

    /// <summary>Finds the nearest shape intersection per owning node, ordered by ascending distance.</summary>
    /// <remarks>Distances are in world units. A zero-length ray returns no hits.</remarks>
    /// <param name="ray">The world-space ray; its direction need not be normalized.</param>
    /// <returns>One hit per intersected node in this scene.</returns>
    IReadOnlyList<RaycastHit> Raycast(Ray3D<float> ray);
}
