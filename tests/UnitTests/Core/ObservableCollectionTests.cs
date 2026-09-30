using Nexus.Core;

namespace Tests;

/// <summary>
/// Tests list behavior and notifications for <see cref="ObservableCollection{T}"/>.
/// </summary>
public class ObservableCollectionTests
{
    /// <summary>Verifies list mutations update items and raise matching notifications.</summary>
    [Fact]
    public void Mutations_updateItemsAndRaiseNotifications()
    {
        var collection = new ObservableCollection<string>();
        var addedItems = new List<string>();
        var removedItems = new List<string>();
        collection.ItemAdded += addedItems.Add;
        collection.ItemRemoved += removedItems.Add;

        collection.Add("first");
        collection.Insert(0, "second");
        collection[1] = "replacement";
        Assert.True(collection.Remove("second"));
        collection.RemoveAt(0);
        collection.Add("third");
        collection.Add("fourth");
        collection.Clear();

        Assert.Empty(collection);
        Assert.Equal(["first", "second", "replacement", "third", "fourth"], addedItems);
        Assert.Equal(["first", "second", "replacement", "third", "fourth"], removedItems);
        Assert.False(collection.Remove("missing"));
    }

    /// <summary>Verifies the collection delegates list operations and exposes a read-only view.</summary>
    [Fact]
    public void ListOperations_andReadOnlyView_shareItems()
    {
        var collection = new ObservableCollection<string> { "first", "second" };
        IReadOnlyObservableCollection<string> readOnly = collection.AsReadOnly();
        var copy = new string[2];

        collection.CopyTo(copy, 0);

        Assert.Same(collection, readOnly);
        Assert.False(collection.IsReadOnly);
        Assert.Equal(2, collection.Count);
        Assert.Equal(1, collection.IndexOf("second"));
        Assert.True(collection.Contains("first"));
        Assert.Equal(["first", "second"], copy);
        Assert.Equal(["first", "second"], readOnly);
    }
}
