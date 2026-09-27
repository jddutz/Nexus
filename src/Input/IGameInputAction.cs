namespace Nexus.Input;

/// <summary>
/// Represents an action that can be executed by a scene input binding.
/// </summary>
public interface IGameInputAction
{
    /// <summary>
    /// Executes the action.
    /// </summary>
    void Execute();
}
