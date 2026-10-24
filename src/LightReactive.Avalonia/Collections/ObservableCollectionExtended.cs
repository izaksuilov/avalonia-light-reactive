using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LightReactive.Avalonia.Collections
{
    /// <summary>
    /// Observable collection with batch operations. Lightweight replacement for DynamicData.Binding.ObservableCollectionExtended.
    /// Batch operations raise a single Reset notification so the UI is not disturbed for every item.
    /// </summary>
    public class ObservableCollectionExtended<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Empty collection.
        /// </summary>
        public ObservableCollectionExtended()
        {
        }

        /// <summary>
        /// Collection with an initial set of items.
        /// </summary>
        public ObservableCollectionExtended(IEnumerable<T> items) : base(items)
        {
        }

        // Batch methods first take a copy of items and only then modify the collection. items is often
        // built from the collection itself (c.RemoveMany(c.Where(...))): lazy enumeration would fail
        // on the first change, leaving the collection half-modified and without a Reset.

        /// <summary>
        /// Adds several items with a single Reset notification.
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items is null)
                return;

            List<T> snapshot = new(items);
            if (snapshot.Count == 0)
                return;

            foreach (T item in snapshot)
                Items.Add(item);

            RaiseReset();
        }

        /// <summary>
        /// Clears the collection and loads a new set of items with a single Reset notification.
        /// </summary>
        public void Load(IEnumerable<T> items)
        {
            List<T>? snapshot = items is null ? null : new(items);

            Items.Clear();
            if (snapshot is not null)
            {
                foreach (T item in snapshot)
                    Items.Add(item);
            }

            RaiseReset();
        }

        /// <summary>
        /// Removes several items.
        /// </summary>
        public void RemoveMany(IEnumerable<T> items)
        {
            if (items is null)
                return;

            bool any = false;
            foreach (T item in new List<T>(items))
                any |= Items.Remove(item);

            if (any)
                RaiseReset();
        }

        private void RaiseReset()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    /// <summary>
    /// Batch extensions for collections. Lightweight replacement for the corresponding DynamicData extensions
    /// (AddRange/RemoveMany) for ordinary collections (AvaloniaList, AvaloniaCollection, etc.).
    /// If the collection has its own AddRange/RemoveMany method, it is called (instance methods take precedence).
    /// </summary>
    public static class ObservableCollectionEx
    {
        /// <summary>
        /// Adds all items to the collection.
        /// </summary>
        public static void AddRange<T>(this ICollection<T> source, IEnumerable<T> items)
        {
            if (source is null || items is null)
                return;

            // Copy, as in RemoveMany: items may be a query over the same collection.
            foreach (T item in new List<T>(items))
                source.Add(item);
        }

        /// <summary>
        /// Removes the specified items from the collection.
        /// </summary>
        public static void RemoveMany<T>(this ICollection<T> source, IEnumerable<T> items)
        {
            if (source is null || items is null)
                return;

            foreach (T item in new List<T>(items))
                source.Remove(item);
        }
    }
}
