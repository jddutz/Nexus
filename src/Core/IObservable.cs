namespace Nexus.Core;

/// <summary>
/// Describes an object that reports property and effective-state changes.
/// </summary>
public interface IObservable
{
    /// <summary>
    /// Occurs when a property has changed.
    /// </summary>
    event Action<string>? PropertyChanged;
}
