namespace Nexus.Core;

/// <summary>Provides typed helpers for observable collections.</summary>
public static class ObservableCollectionExtensions
{
    /// <summary>Adds a newly created item to the collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="collection">The target collection.</param>
    /// <returns>The added item.</returns>
    public static T Add<T>(this IObservableCollection<T> collection)
        where T : new()
    {
        var item = new T();
        collection.Add(item);
        return item;
    }

    /// <summary>Creates and adds a component to a component collection.</summary>
    /// <typeparam name="TComponent">The component type.</typeparam>
    /// <param name="collection">The target component collection.</param>
    /// <returns>The added component.</returns>
    public static TComponent Add<TComponent>(this IObservableCollection<IComponent> collection)
        where TComponent : class, IComponent, new()
    {
        var component = new TComponent();
        collection.Add(component);
        return component;
    }

    /// <summary>Gets the first item of the requested type.</summary>
    /// <typeparam name="TItem">The requested item type.</typeparam>
    /// <param name="collection">The source collection.</param>
    /// <returns>The matching item, or <see langword="null"/>.</returns>
    public static TItem? Get<TItem>(this IEnumerable<IComponent> collection)
        where TItem : class, IComponent => collection.OfType<TItem>().FirstOrDefault();

    /// <summary>Gets the first item assignable to the requested type.</summary>
    /// <typeparam name="TItem">The requested item type.</typeparam>
    /// <typeparam name="TBase">The collection item type.</typeparam>
    /// <param name="collection">The source collection.</param>
    /// <returns>The matching item, or <see langword="null"/>.</returns>
    /// <summary>Removes the first item of the requested type.</summary>
    /// <typeparam name="TItem">The requested item type.</typeparam>
    /// <param name="collection">The target collection.</param>
    /// <returns><see langword="true"/> when an item was removed.</returns>
    public static bool Remove<TItem>(this IObservableCollection<IComponent> collection)
        where TItem : class, IComponent
    {
        var item = collection.OfType<TItem>().FirstOrDefault();
        return item is not null && collection.Remove(item);
    }
}
