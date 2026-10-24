namespace LightReactive.Avalonia
{
    /// <summary>
    /// Common base for subjects: subscriber list, thread-safe broadcast, and disposal.
    /// Notifications are delivered to a snapshot of subscribers; user callbacks are invoked outside the lock.
    /// Storing and immediately replaying the latest value is configured by derived classes through hooks.
    /// </summary>
    public abstract class SubjectBase<T> : IObservable<T>, IObserver<T>, IDisposable
    {
        private readonly object _gate = new();
        private List<IObserver<T>>? _observers = new();
        private bool _completed;
        private Exception? _error;
        private bool _disposed;

        /// <summary>
        /// Indicates whether there are any subscribers.
        /// </summary>
        public bool HasObservers
        {
            get { lock (_gate) return _observers is { Count: > 0 }; }
        }

        /// <summary>
        /// Indicates whether the subject has been disposed.
        /// </summary>
        public bool IsDisposed
        {
            get { lock (_gate) return _disposed; }
        }

        /// <summary>
        /// Returns the subject as an observable (hides the observer interface). Replacement for System.Reactive AsObservable.
        /// </summary>
        public IObservable<T> AsObservable() => this;

        /// <inheritdoc />
        public void OnNext(T value)
        {
            IObserver<T>[] targets;
            lock (_gate)
            {
                // After Dispose _observers == null - publishing is silently ignored (no ObjectDisposedException).
                if (_completed || _error is not null || _observers is null)
                    return;

                StoreValue(value);
                targets = _observers.ToArray();
            }

            foreach (IObserver<T> o in targets)
                o.OnNext(value);
        }

        /// <inheritdoc />
        public void OnError(Exception error)
        {
            IObserver<T>[] targets;
            lock (_gate)
            {
                // After Dispose _observers == null - publishing is silently ignored (no ObjectDisposedException).
                if (_completed || _error is not null || _observers is null)
                    return;

                _error = error;
                targets = _observers.ToArray();
                _observers = null;
            }

            foreach (IObserver<T> o in targets)
                o.OnError(error);
        }

        /// <inheritdoc />
        public void OnCompleted()
        {
            IObserver<T>[] targets;
            lock (_gate)
            {
                // After Dispose _observers == null - publishing is silently ignored (no ObjectDisposedException).
                if (_completed || _error is not null || _observers is null)
                    return;

                _completed = true;
                targets = _observers.ToArray();
                _observers = null;
            }

            foreach (IObserver<T> o in targets)
                o.OnCompleted();
        }

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<T> observer)
        {
            Exception? error;
            bool completed;
            bool replay = false;
            T initial = default!;
            lock (_gate)
            {
                CheckDisposed();

                error = _error;
                completed = _completed;
                if (error is null && !completed)
                {
                    _observers ??= new();
                    _observers.Add(observer);
                    replay = TryGetReplayValue(out initial);
                }
            }

            // Terminal notifications and value replay are delivered outside the lock so that
            // a user callback does not run under _gate (risk of deadlock/reentrancy).
            if (error is not null)
            {
                observer.OnError(error);
                return Disposable.Empty;
            }

            if (completed)
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }

            if (replay)
                observer.OnNext(initial);

            return new Subscription(this, observer);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _observers = null;
            }
        }

        /// <summary>
        /// Stores the value under the lock before broadcasting. Does nothing by default.
        /// </summary>
        private protected virtual void StoreValue(T value)
        {
        }

        /// <summary>
        /// Returns the value to replay immediately to a new subscriber. No value by default.
        /// Called under the lock.
        /// </summary>
        private protected virtual bool TryGetReplayValue(out T value)
        {
            value = default!;
            return false;
        }

        /// <summary>
        /// Current error of the subject. Read only under <see cref="Gate"/>.
        /// </summary>
        private protected Exception? ErrorNoLock => _error;

        /// <summary>
        /// State lock object of the subject.
        /// </summary>
        private protected object Gate => _gate;

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if the subject is disposed. Call under <see cref="Gate"/>.
        /// </summary>
        private protected void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        private void Unsubscribe(IObserver<T> observer)
        {
            lock (_gate)
            {
                _observers?.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private SubjectBase<T>? _subject;
            private readonly IObserver<T> _observer;

            public Subscription(SubjectBase<T> subject, IObserver<T> observer)
            {
                _subject = subject;
                _observer = observer;
            }

            public void Dispose()
            {
                SubjectBase<T>? subject = Interlocked.Exchange(ref _subject, null);
                subject?.Unsubscribe(_observer);
            }
        }
    }

    /// <summary>
    /// Observable sequence that values can be pushed into and that can be subscribed to.
    /// Replacement for System.Reactive.Subjects.Subject. Thread-safe; notifications are delivered to a snapshot of subscribers.
    /// </summary>
    public class Subject<T> : SubjectBase<T>
    {
        // All logic lives in SubjectBase: a plain subject does not store a value and replays nothing on subscribe.
    }

    /// <summary>
    /// Observable sequence that stores the latest value. A new subscriber immediately receives the current value.
    /// Replacement for System.Reactive.Subjects.BehaviorSubject. Thread-safe; notifications are delivered to a snapshot of subscribers.
    /// </summary>
    public sealed class BehaviorSubject<T> : SubjectBase<T>
    {
        private T _value;

        /// <summary>
        /// Constructor with an initial value.
        /// </summary>
        public BehaviorSubject(T value) => _value = value;

        /// <summary>
        /// Current value.
        /// </summary>
        public T Value
        {
            get
            {
                lock (Gate)
                {
                    CheckDisposed();
                    if (ErrorNoLock is not null)
                        throw ErrorNoLock;

                    return _value;
                }
            }
        }

        private protected override void StoreValue(T value) => _value = value;

        private protected override bool TryGetReplayValue(out T value)
        {
            value = _value;
            return true;
        }
    }
}
