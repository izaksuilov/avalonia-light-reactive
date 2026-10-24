using System.Collections.ObjectModel;
using System.Collections.Specialized;
using AvaloniaLight.Reactive.Collections;

namespace AvaloniaLight.Reactive.Tests;

/// <summary>
/// Tests for batch collection operations: one Reset notification per operation and correct behavior
/// when the set of items is built by a query over the same collection.
/// </summary>
public class ObservableCollectionExtendedTests
{
    private static (List<NotifyCollectionChangedAction> Collection, List<string?> Properties) Record<T>(ObservableCollectionExtended<T> collection)
    {
        List<NotifyCollectionChangedAction> actions = new();
        List<string?> properties = new();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);
        ((System.ComponentModel.INotifyPropertyChanged)collection).PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        return (actions, properties);
    }

    [Fact]
    public void AddRange_Items_AddsWithSingleReset()
    {
        ObservableCollectionExtended<int> collection = new([1]);
        var (actions, properties) = Record(collection);

        collection.AddRange([2, 3]);

        Assert.Equal(new[] { 1, 2, 3 }, collection);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, actions);
        Assert.Equal(new[] { "Count", "Item[]" }, properties);
    }

    [Fact]
    public void AddRange_EmptyOrNull_NoNotification()
    {
        ObservableCollectionExtended<int> collection = new([1]);
        var (actions, _) = Record(collection);

        collection.AddRange([]);
        collection.AddRange(null!);

        Assert.Equal(new[] { 1 }, collection);
        Assert.Empty(actions);
    }

    [Fact]
    public void AddRange_QueryOverSameCollection_AddsSnapshot()
    {
        ObservableCollectionExtended<int> collection = new([1, 2, 3]);
        var (actions, _) = Record(collection);

        collection.AddRange(collection.Where(x => x > 1));

        Assert.Equal(new[] { 1, 2, 3, 2, 3 }, collection);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, actions);
    }

    [Fact]
    public void RemoveMany_QueryOverSameCollection_RemovesWithSingleReset()
    {
        ObservableCollectionExtended<int> collection = new([1, 2, 3, 4]);
        var (actions, _) = Record(collection);

        collection.RemoveMany(collection.Where(x => x % 2 == 0));

        Assert.Equal(new[] { 1, 3 }, collection);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, actions);
    }

    [Fact]
    public void RemoveMany_NoMatches_NoNotification()
    {
        ObservableCollectionExtended<int> collection = new([1, 2]);
        var (actions, _) = Record(collection);

        collection.RemoveMany([5]);
        collection.RemoveMany(null!);

        Assert.Equal(new[] { 1, 2 }, collection);
        Assert.Empty(actions);
    }

    [Fact]
    public void Load_QueryOverSameCollection_ReplacesWithSnapshot()
    {
        ObservableCollectionExtended<int> collection = new([1, 2, 3, 4]);
        var (actions, _) = Record(collection);

        collection.Load(collection.Where(x => x % 2 == 0));

        Assert.Equal(new[] { 2, 4 }, collection);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, actions);
    }

    [Fact]
    public void Load_Null_ClearsWithReset()
    {
        ObservableCollectionExtended<int> collection = new([1, 2]);
        var (actions, _) = Record(collection);

        collection.Load(null!);

        Assert.Empty(collection);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, actions);
    }

    [Fact]
    public void CollectionExtensions_QueryOverSameCollection_UseSnapshot()
    {
        // Collection<T> has no AddRange/RemoveMany of its own: the ObservableCollectionEx extensions are called.
        Collection<int> collection = new() { 1, 2, 3, 4 };

        collection.AddRange(collection.Where(x => x > 2));
        collection.RemoveMany(collection.Where(x => x % 2 == 0));

        Assert.Equal(new[] { 1, 3, 3 }, collection);
    }
}
