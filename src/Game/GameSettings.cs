namespace Nexus.Game;

/// <summary>
/// Settings used by runtime startup.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Gets or sets the identifier of the scene loaded at startup when multiple scenes are registered.</summary>
    public string? StartSceneId { get; set; }
}
