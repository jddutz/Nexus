namespace Nexus.Input.Devices;

/// <summary>Represents one two-dimensional analog control on a controller.</summary>
public interface IAnalogInput
{
    /// <summary>Gets the controller-local logical index.</summary>
    int Index { get; }

    /// <summary>Gets the normalized physical role, or <see langword="null"/> when unknown; built-in values are documented by <see cref="ControllerSemanticNames"/>.</summary>
    string? SemanticName { get; }

    /// <summary>Gets the current position; one-axis inputs keep the Y component at zero.</summary>
    Vector2D<float> Position { get; }
}
