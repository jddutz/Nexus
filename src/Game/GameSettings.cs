namespace Nexus.Game;

/// <summary>
/// Settings that configure the game system.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Gets or sets the identifier of the scene to start.</summary>
    public string? StartSceneId { get; set; }
}
