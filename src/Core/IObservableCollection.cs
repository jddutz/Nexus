namespace Nexus.Core;

/// <summary>
/// Defines a mutable list that notifies when items are added or removed.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public interface IObservableCollection<T> : IList<T>
{
    /// <summary>
    /// Occurs when an item is added to the collection.
    /// </summary>
    event Action<T>? ItemAdded;

    /// <summary>
    /// Occurs when an item is removed from the collection.
    /// </summary>
    event Action<T>? ItemRemoved;

    /// <summary>
    /// Gets this collection as a read-only collection.
    /// </summary>
    /// <returns>A read-only representation of this collection.</returns>
    IReadOnlyObservableCollection<T> AsReadOnly();
}
