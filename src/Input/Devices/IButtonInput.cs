namespace Nexus.Input.Devices;

/// <summary>Represents a binary control on one controller.</summary>
public interface IButtonInput
{
    /// <summary>Gets the controller-local logical index.</summary>
    int Index { get; }

    /// <summary>Gets the normalized physical role, or <see langword="null"/> when unknown; built-in values are documented by <see cref="ControllerSemanticNames"/>.</summary>
    string? SemanticName { get; }

    /// <summary>Gets whether this button is currently pressed.</summary>
    bool IsPressed { get; }
}
