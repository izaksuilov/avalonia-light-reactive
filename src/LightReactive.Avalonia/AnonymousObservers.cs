namespace LightReactive.Avalonia
{
    /// <summary>
    /// Delegate-based observer. Replacement for System.Reactive.AnonymousObserver.
    /// </summary>
    public sealed class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        private readonly Action<Exception>? _onError;
        private readonly Action? _onCompleted;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AnonymousObserver(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
        {
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        /// <inheritdoc />
        public void OnNext(T value) => _onNext(value);

        /// <inheritdoc />
        public void OnError(Exception error) => _onError?.Invoke(error);

        /// <inheritdoc />
        public void OnCompleted() => _onCompleted?.Invoke();
    }

    /// <summary>
    /// Observable sequence based on a subscribe delegate. Replacement for System.Reactive.AnonymousObservable.
    /// The Rx contract is enforced on behalf of the delegate: after OnError, OnCompleted, or unsubscription the observer no longer
    /// receives anything, and the subscription returned by the delegate is disposed.
    /// </summary>
    public sealed class AnonymousObservable<T> : IObservable<T>
    {
        private readonly Func<IObserver<T>, IDisposable> _subscribe;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AnonymousObservable(Func<IObserver<T>, IDisposable> subscribe) => _subscribe = subscribe;

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<T> observer)
        {
            AutoDetachObserver<T> sink = new(observer);
            sink.SetResource(_subscribe(sink));
            return sink;
        }
    }

    /// <summary>
    /// Wrapper observer for <see cref="AnonymousObservable{T}"/>. Lets one terminal notification through
    /// and nothing after it or after unsubscription. On a terminal notification it disposes the delegate's subscription,
    /// even if the source emitted it synchronously, before the delegate returned: in that case the subscription is disposed
    /// immediately after assignment.
    /// </summary>
    internal sealed class AutoDetachObserver<T> : IObserver<T>, IDisposable
    {
        // Marker for "subscription already disposed": a subscription assigned after it is disposed immediately.
        private static readonly IDisposable DisposedMarker = new BooleanDisposable();

        private readonly IObserver<T> _observer;
        private IDisposable? _resource;
        private int _stopped;

        public AutoDetachObserver(IObserver<T> observer) => _observer = observer;

        public void SetResource(IDisposable? resource)
        {
            if (Interlocked.CompareExchange(ref _resource, resource, null) == DisposedMarker)
                resource?.Dispose();
        }

        public void OnNext(T value)
        {
            if (Volatile.Read(ref _stopped) == 0)
                _observer.OnNext(value);
        }

        public void OnError(Exception error)
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            try { _observer.OnError(error); }
            finally { DisposeResource(); }
        }

        public void OnCompleted()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;

            try { _observer.OnCompleted(); }
            finally { DisposeResource(); }
        }

        public void Dispose()
        {
            Volatile.Write(ref _stopped, 1);
            DisposeResource();
        }

        private void DisposeResource()
        {
            IDisposable? resource = Interlocked.Exchange(ref _resource, DisposedMarker);
            if (resource != DisposedMarker)
                resource?.Dispose();
        }
    }
}
