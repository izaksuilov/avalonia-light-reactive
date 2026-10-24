namespace AvaloniaLight.Reactive
{
    /// <summary>
    /// Holds a single replaceable disposable. Setting a new one disposes the old one.
    /// Replacement for System.Reactive.Disposables.SerialDisposable. Thread-safe.
    /// </summary>
    public sealed class SerialDisposable : IDisposable
    {
        private readonly object _gate = new();
        private IDisposable? _current;
        private bool _disposed;

        /// <summary>
        /// Current inner object. Setting it disposes the previous one.
        /// If the container is already disposed, the new value is disposed immediately.
        /// </summary>
        public IDisposable? Disposable
        {
            get
            {
                lock (_gate) return _current;
            }
            set
            {
                IDisposable? old;
                bool disposeValueNow;
                lock (_gate)
                {
                    disposeValueNow = _disposed;
                    old = disposeValueNow ? null : _current;
                    if (!disposeValueNow)
                        _current = value;
                }

                old?.Dispose();
                if (disposeValueNow)
                    value?.Dispose();
            }
        }

        /// <summary>
        /// Indicates whether the container has already been disposed.
        /// </summary>
        public bool IsDisposed
        {
            get { lock (_gate) return _disposed; }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            IDisposable? old;
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                old = _current;
                _current = null;
            }

            old?.Dispose();
        }
    }

    /// <summary>
    /// Holds a single disposable that can be assigned exactly once.
    /// Replacement for System.Reactive.Disposables.SingleAssignmentDisposable. Thread-safe.
    /// </summary>
    public sealed class SingleAssignmentDisposable : IDisposable
    {
        private readonly object _gate = new();
        private IDisposable? _current;
        private bool _disposed;

        /// <summary>
        /// Inner object. Reassignment is not allowed. If the container is already disposed,
        /// the new value is disposed immediately.
        /// </summary>
        public IDisposable? Disposable
        {
            get
            {
                lock (_gate) return _current;
            }
            set
            {
                bool disposeValueNow;
                lock (_gate)
                {
                    disposeValueNow = _disposed;
                    if (!disposeValueNow)
                    {
                        if (_current is not null)
                            throw new InvalidOperationException("The disposable has already been assigned.");

                        _current = value;
                    }
                }

                if (disposeValueNow)
                    value?.Dispose();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            IDisposable? old;
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                old = _current;
                _current = null;
            }

            old?.Dispose();
        }
    }
}
