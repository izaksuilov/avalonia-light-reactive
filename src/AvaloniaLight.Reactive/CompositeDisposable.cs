using System.Collections;

namespace AvaloniaLight.Reactive
{
    /// <summary>
    /// Container of disposables. Disposes all contained items.
    /// Replacement for System.Reactive.Disposables.CompositeDisposable. Thread-safe.
    /// </summary>
    public sealed class CompositeDisposable : ICollection<IDisposable>, IDisposable
    {
        private readonly object _gate = new();
        private readonly List<IDisposable> _disposables;
        private bool _disposed;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CompositeDisposable() => _disposables = new();

        /// <summary>
        /// Constructor with a given capacity.
        /// </summary>
        public CompositeDisposable(int capacity) => _disposables = new(capacity);

        /// <summary>
        /// Constructor with a set of items.
        /// </summary>
        public CompositeDisposable(IEnumerable<IDisposable> disposables) => _disposables = new(disposables);

        /// <summary>
        /// Constructor with a set of items.
        /// </summary>
        public CompositeDisposable(params IDisposable[] disposables) => _disposables = new(disposables);

        /// <summary>
        /// Number of items in the container.
        /// </summary>
        public int Count
        {
            get { lock (_gate) return _disposables.Count; }
        }

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <summary>
        /// Indicates whether the container has already been disposed.
        /// </summary>
        public bool IsDisposed
        {
            get { lock (_gate) return _disposed; }
        }

        /// <summary>
        /// Adds an item. If the container is already disposed, the item is disposed immediately.
        /// </summary>
        public void Add(IDisposable item)
        {
            if (item is null)
                return;

            lock (_gate)
            {
                if (!_disposed)
                {
                    _disposables.Add(item);
                    return;
                }
            }

            item.Dispose();
        }

        /// <summary>
        /// Removes the item and disposes it.
        /// </summary>
        public bool Remove(IDisposable item)
        {
            if (item is null)
                return false;

            lock (_gate)
            {
                if (_disposed || !_disposables.Remove(item))
                    return false;
            }

            item.Dispose();
            return true;
        }

        /// <summary>
        /// Disposes and removes all items (the container remains usable).
        /// </summary>
        public void Clear()
        {
            IDisposable[] items;
            lock (_gate)
                items = SnapshotAndClearLocked();

            foreach (IDisposable d in items)
                d.Dispose();
        }

        /// <inheritdoc />
        public bool Contains(IDisposable item)
        {
            lock (_gate) return _disposables.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(IDisposable[] array, int arrayIndex)
        {
            lock (_gate) _disposables.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Disposes the container and all contained items. Repeated calls are safe.
        /// </summary>
        public void Dispose()
        {
            IDisposable[] items;
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                items = SnapshotAndClearLocked();
            }

            foreach (IDisposable d in items)
                d.Dispose();
        }

        // Takes a snapshot of the items and clears the list. Call under _gate.
        private IDisposable[] SnapshotAndClearLocked()
        {
            IDisposable[] items = _disposables.ToArray();
            _disposables.Clear();
            return items;
        }

        /// <inheritdoc />
        public IEnumerator<IDisposable> GetEnumerator()
        {
            IDisposable[] items;
            lock (_gate) items = _disposables.ToArray();
            return ((IEnumerable<IDisposable>)items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
