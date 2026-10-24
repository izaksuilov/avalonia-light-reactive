namespace LightReactive.Avalonia
{
    /// <summary>
    /// Factory for disposable objects. Replacement for System.Reactive.Disposables.Disposable.
    /// </summary>
    public static class Disposable
    {
        /// <summary>
        /// An empty disposable that does nothing.
        /// </summary>
        public static IDisposable Empty { get; } = new EmptyDisposable();

        /// <summary>
        /// Creates a disposable that invokes the action on disposal (exactly once).
        /// </summary>
        public static IDisposable Create(Action dispose) => new AnonymousDisposable(dispose);

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// Action-based disposable. The action is invoked at most once.
    /// </summary>
    public sealed class AnonymousDisposable : IDisposable
    {
        private Action? _dispose;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AnonymousDisposable(Action dispose) => _dispose = dispose;

        /// <summary>
        /// Indicates whether the object has already been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _dispose) is null;

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }

    /// <summary>
    /// Disposable with a disposed flag. Replacement for System.Reactive.Disposables.BooleanDisposable.
    /// </summary>
    public sealed class BooleanDisposable : IDisposable
    {
        private volatile bool _isDisposed;

        /// <summary>
        /// Indicates whether the object has already been disposed.
        /// </summary>
        public bool IsDisposed => _isDisposed;

        /// <inheritdoc />
        public void Dispose() => _isDisposed = true;
    }

    /// <summary>
    /// Extensions for disposables.
    /// </summary>
    public static class DisposableMixins
    {
        /// <summary>
        /// Adds the object to a shared container for joint disposal. Replacement for DisposeWith from System.Reactive.
        /// </summary>
        public static T DisposeWith<T>(this T item, CompositeDisposable compositeDisposable) where T : IDisposable
        {
            compositeDisposable.Add(item);
            return item;
        }
    }
}
