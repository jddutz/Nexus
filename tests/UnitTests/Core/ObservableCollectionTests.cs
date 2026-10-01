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

    /// <summary>Verifies validation handlers reject additions and replacements before mutation.</summary>
    [Fact]
    public void Validation_rejectsMutationsBeforeChangingCollection()
    {
        var collection = new ObservableCollection<string> { "first", "second" };
        var addedItems = new List<string>();
        var removedItems = new List<string>();
        var laterRuleCalls = 0;
        collection.ItemAdded += addedItems.Add;
        collection.ItemRemoved += removedItems.Add;
        collection.ValidationRules += item => item != "blocked" && item != "rejected";
        collection.ValidationRules += _ =>
        {
            laterRuleCalls++;
            return true;
        };

        Assert.Throws<InvalidOperationException>(() => collection.Add("blocked"));
        Assert.Throws<InvalidOperationException>(() => collection.Insert(0, "blocked"));
        Assert.Throws<InvalidOperationException>(() => collection[0] = "blocked");
        Assert.Throws<InvalidOperationException>(() => collection[1] = "rejected");

        Assert.Equal(["first", "second"], collection);
        Assert.Empty(addedItems);
        Assert.Empty(removedItems);
        Assert.Equal(0, laterRuleCalls);
    }

    /// <summary>Verifies removal operations do not invoke incoming-item validation rules.</summary>
    [Fact]
    public void ValidationRules_doNotRestrictRemovals()
    {
        var collection = new ObservableCollection<string> { "first", "second", "third" };
        var removedItems = new List<string>();
        collection.ItemRemoved += removedItems.Add;
        collection.ValidationRules += static _ => false;

        Assert.True(collection.Remove("first"));
        collection.RemoveAt(0);
        collection.Clear();

        Assert.Empty(collection);
        Assert.Equal(["first", "second", "third"], removedItems);
    }

    /// <summary>Verifies assigning the same object does not invoke validation or raise events.</summary>
    [Fact]
    public void Indexer_assignmentOfSameInstance_isNoOpBeforeValidation()
    {
        var item = new object();
        var collection = new ObservableCollection<object> { item };
        var addedItems = new List<object>();
        var removedItems = new List<object>();
        collection.ItemAdded += addedItems.Add;
        collection.ItemRemoved += removedItems.Add;
        collection.ValidationRules += static _ => false;

        collection[0] = item;

        Assert.Same(item, collection[0]);
        Assert.Empty(addedItems);
        Assert.Empty(removedItems);
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
        Assert.Contains("first", collection);
        Assert.Equal(["first", "second"], copy);
        Assert.Equal(["first", "second"], readOnly);
    }
}
