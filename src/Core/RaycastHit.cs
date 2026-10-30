namespace Nexus.Core;

/// <summary>Describes the nearest ray intersection with a scene node's shapes.</summary>
/// <param name="Node">The node that owns the intersected shape.</param>
/// <param name="Distance">The distance from the ray origin in world units.</param>
/// <remarks>
/// Reconstruct the world point as origin + normalized direction * distance.
/// </remarks>
public readonly record struct RaycastHit(ISceneNode Node, float Distance);
