namespace Nexus.Game;

/// <summary>
/// Settings that configure the game system.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Gets or sets the name of the initial scene.</summary>
    public string InitialScene { get; set; } = string.Empty;
}
