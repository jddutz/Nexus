namespace Nexus.Core;

/// <summary>
/// Defines a read-only list that notifies when items are added or removed.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public interface IReadOnlyObservableCollection<T> : IReadOnlyList<T>
{
    /// <summary>
    /// Occurs when an item is added to the collection.
    /// </summary>
    event Action<T>? ItemAdded;

    /// <summary>
    /// Occurs when an item is removed from the collection.
    /// </summary>
    event Action<T>? ItemRemoved;
}
