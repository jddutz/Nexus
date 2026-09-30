namespace Nexus.Core;

/// <summary>
/// Provides a mutable list that raises notifications when items are added or removed.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public class ObservableCollection<T> : IObservableCollection<T>, IReadOnlyObservableCollection<T>
{
    private readonly List<T> _items = [];

    /// <inheritdoc />
    public event Action<T>? ItemAdded;

    /// <inheritdoc />
    public event Action<T>? ItemRemoved;

    /// <inheritdoc />
    public T this[int index]
    {
        get => _items[index];
        set
        {
            var removedItem = _items[index];
            _items[index] = value;
            ItemRemoved?.Invoke(removedItem);
            ItemAdded?.Invoke(value);
        }
    }

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public IReadOnlyObservableCollection<T> AsReadOnly() => this;

    /// <inheritdoc />
    public void Add(T item)
    {
        _items.Add(item);
        ItemAdded?.Invoke(item);
    }

    /// <inheritdoc />
    public void Clear()
    {
        var removedItems = _items.ToArray();
        _items.Clear();

        foreach (var item in removedItems)
            ItemRemoved?.Invoke(item);
    }

    /// <inheritdoc />
    public bool Contains(T item) => _items.Contains(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    public int IndexOf(T item) => _items.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        ItemAdded?.Invoke(item);
    }

    /// <inheritdoc />
    public bool Remove(T item)
    {
        var index = _items.IndexOf(item);
        if (index < 0)
            return false;

        var removedItem = _items[index];
        _items.RemoveAt(index);
        ItemRemoved?.Invoke(removedItem);
        return true;
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        var removedItem = _items[index];
        _items.RemoveAt(index);
        ItemRemoved?.Invoke(removedItem);
    }

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
