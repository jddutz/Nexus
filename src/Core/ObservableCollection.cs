namespace Nexus.Core;

/// <summary>
/// Provides a mutable list that raises notifications when items are added or removed.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public class ObservableCollection<T> : IObservableCollection<T>, IReadOnlyObservableCollection<T>
{
    private readonly List<T> _items = [];
    private readonly IEqualityComparer<T> _equalityComparer;

    /// <summary>Initializes a collection using the default equality comparer.</summary>
    public ObservableCollection()
        : this(EqualityComparer<T>.Default) { }

    /// <summary>Initializes a collection using the specified equality comparer.</summary>
    /// <param name="equalityComparer">The comparer used for membership and removal.</param>
    public ObservableCollection(IEqualityComparer<T> equalityComparer)
    {
        ArgumentNullException.ThrowIfNull(equalityComparer);
        _equalityComparer = equalityComparer;
    }

    /// <inheritdoc />
    public event Action<T>? ItemAdded;

    /// <inheritdoc />
    public event Action<T>? ItemRemoved;

    /// <inheritdoc />
    public event Predicate<T>? ValidationRules;

    /// <inheritdoc />
    public T this[int index]
    {
        get => _items[index];
        set
        {
            var removedItem = _items[index];
            if (ReferenceEquals(removedItem, value))
                return;

            Validate(value);
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
        Validate(item);
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
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    public int IndexOf(T item) =>
        _items.FindIndex(candidate => _equalityComparer.Equals(candidate, item));

    /// <inheritdoc />
    public void Insert(int index, T item)
    {
        Validate(item);
        _items.Insert(index, item);
        ItemAdded?.Invoke(item);
    }

    /// <inheritdoc />
    public bool Remove(T item)
    {
        var index = IndexOf(item);
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

    /// <summary>
    /// Throws when an item does not pass the configured validation predicate.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    private void Validate(T item)
    {
        var validationHandlers = ValidationRules;
        if (validationHandlers is null)
            return;

        foreach (var handler in validationHandlers.GetInvocationList())
        {
            if (!((Predicate<T>)handler)(item))
                throw new InvalidOperationException("The item failed collection validation.");
        }
    }

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
