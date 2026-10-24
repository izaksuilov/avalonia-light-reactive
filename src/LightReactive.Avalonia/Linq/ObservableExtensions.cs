namespace LightReactive.Avalonia.Linq
{
    /// <summary>
    /// Operators over observable sequences. Replacement for the subset of System.Reactive.Linq in use.
    /// Lives in the .Linq namespace: while coexisting with the System.Reactive package, files
    /// import this namespace only after the package is removed (otherwise Subscribe from System conflicts).
    /// </summary>
    public static class ObservableExtensions
    {
        #region Subscribe

        /// <summary>
        /// Subscribes with a value handler.
        /// </summary>
        public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext)
            => source.Subscribe(new AnonymousObserver<T>(onNext));

        /// <summary>
        /// Subscribes with value and error handlers.
        /// </summary>
        public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError)
            => source.Subscribe(new AnonymousObserver<T>(onNext, onError));

        /// <summary>
        /// Subscribes with value and completion handlers.
        /// </summary>
        public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action onCompleted)
            => source.Subscribe(new AnonymousObserver<T>(onNext, null, onCompleted));

        /// <summary>
        /// Subscribes with all handlers.
        /// </summary>
        public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError, Action onCompleted)
            => source.Subscribe(new AnonymousObserver<T>(onNext, onError, onCompleted));

        #endregion

        #region Simple operators

        /// <summary>
        /// Filters out values that do not satisfy the condition.
        /// </summary>
        public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, bool> predicate)
            => new AnonymousObservable<T>(observer =>
            {
                SingleAssignmentDisposable subscription = new();
                subscription.Disposable = source.Subscribe(new AnonymousObserver<T>(
                    value =>
                    {
                        bool pass;
                        // An exception from the predicate is delivered as OnError and the source is unsubscribed immediately:
                        // OnError is terminal, so no further values must arrive.
                        try { pass = predicate(value); }
                        catch (Exception ex) { observer.OnError(ex); subscription.Dispose(); return; }
                        if (pass)
                            observer.OnNext(value);
                    },
                    observer.OnError,
                    observer.OnCompleted));
                return subscription;
            });

        /// <summary>
        /// Projects values.
        /// </summary>
        public static IObservable<TResult> Select<T, TResult>(this IObservable<T> source, Func<T, TResult> selector)
            => new AnonymousObservable<TResult>(observer =>
            {
                SingleAssignmentDisposable subscription = new();
                subscription.Disposable = source.Subscribe(new AnonymousObserver<T>(
                    value =>
                    {
                        TResult result;
                        // An exception from the projection is delivered as OnError and the source is unsubscribed: OnError is terminal.
                        try { result = selector(value); }
                        catch (Exception ex) { observer.OnError(ex); subscription.Dispose(); return; }
                        observer.OnNext(result);
                    },
                    observer.OnError,
                    observer.OnCompleted));
                return subscription;
            });

        /// <summary>
        /// Skips the first count values.
        /// </summary>
        public static IObservable<T> Skip<T>(this IObservable<T> source, int count)
            => new AnonymousObservable<T>(observer =>
            {
                int remaining = count;
                return source.Subscribe(new AnonymousObserver<T>(
                    value =>
                    {
                        if (remaining > 0)
                            remaining--;
                        else
                            observer.OnNext(value);
                    },
                    observer.OnError,
                    observer.OnCompleted));
            });

        /// <summary>
        /// Takes the first count values and completes.
        /// </summary>
        public static IObservable<T> Take<T>(this IObservable<T> source, int count)
        {
            if (count <= 0)
                return Observable.Empty<T>();

            return new AnonymousObservable<T>(observer =>
            {
                int remaining = count;
                SingleAssignmentDisposable subscription = new();
                subscription.Disposable = source.Subscribe(new AnonymousObserver<T>(
                    value =>
                    {
                        if (remaining <= 0)
                            return;

                        remaining--;
                        observer.OnNext(value);
                        if (remaining == 0)
                        {
                            observer.OnCompleted();
                            subscription.Dispose();
                        }
                    },
                    observer.OnError,
                    observer.OnCompleted));
                return subscription;
            });
        }

        /// <summary>
        /// Suppresses consecutive duplicate values.
        /// </summary>
        public static IObservable<T> DistinctUntilChanged<T>(this IObservable<T> source)
            => DistinctUntilChanged(source, EqualityComparer<T>.Default);

        /// <summary>
        /// Suppresses consecutive duplicate values using the given comparer.
        /// </summary>
        public static IObservable<T> DistinctUntilChanged<T>(this IObservable<T> source, IEqualityComparer<T> comparer)
            => new AnonymousObservable<T>(observer =>
            {
                bool hasValue = false;
                T? last = default;
                return source.Subscribe(new AnonymousObserver<T>(
                    value =>
                    {
                        if (hasValue && comparer.Equals(last!, value))
                            return;

                        hasValue = true;
                        last = value;
                        observer.OnNext(value);
                    },
                    observer.OnError,
                    observer.OnCompleted));
            });

        /// <summary>
        /// Emits the specified value before the source values.
        /// </summary>
        public static IObservable<T> StartWith<T>(this IObservable<T> source, T value)
            => new AnonymousObservable<T>(observer =>
            {
                observer.OnNext(value);
                return source.Subscribe(observer);
            });

        /// <summary>
        /// Performs a side effect for each value.
        /// </summary>
        public static IObservable<T> Do<T>(this IObservable<T> source, Action<T> onNext)
            => new AnonymousObservable<T>(observer =>
            {
                SingleAssignmentDisposable subscription = new();
                subscription.Disposable = source.Subscribe(new AnonymousObserver<T>(
                    value =>
                    {
                        // An exception from the side effect is delivered as OnError and the source is unsubscribed: OnError is terminal.
                        try { onNext(value); }
                        catch (Exception ex) { observer.OnError(ex); subscription.Dispose(); return; }
                        observer.OnNext(value);
                    },
                    observer.OnError,
                    observer.OnCompleted));
                return subscription;
            });

        #endregion

        #region Combining operators

        /// <summary>
        /// Merges two streams into one (values from both).
        /// </summary>
        public static IObservable<T> Merge<T>(this IObservable<T> first, IObservable<T> second)
            => new AnonymousObservable<T>(observer =>
            {
                object gate = new();
                bool done1 = false, done2 = false;

                IDisposable s1 = first.Subscribe(new AnonymousObserver<T>(
                    value => { lock (gate) observer.OnNext(value); },
                    ex => { lock (gate) observer.OnError(ex); },
                    () =>
                    {
                        bool complete;
                        lock (gate) { done1 = true; complete = done2; }
                        if (complete) observer.OnCompleted();
                    }));

                IDisposable s2 = second.Subscribe(new AnonymousObserver<T>(
                    value => { lock (gate) observer.OnNext(value); },
                    ex => { lock (gate) observer.OnError(ex); },
                    () =>
                    {
                        bool complete;
                        lock (gate) { done2 = true; complete = done1; }
                        if (complete) observer.OnCompleted();
                    }));

                return new CompositeDisposable(s1, s2);
            });

        /// <summary>
        /// Combines the latest values of two streams using a combiner function.
        /// </summary>
        public static IObservable<TResult> CombineLatest<TFirst, TSecond, TResult>(
            this IObservable<TFirst> first,
            IObservable<TSecond> second,
            Func<TFirst, TSecond, TResult> resultSelector)
            => new AnonymousObservable<TResult>(observer =>
            {
                object gate = new();
                bool has1 = false, has2 = false;
                bool done1 = false, done2 = false;
                TFirst v1 = default!;
                TSecond v2 = default!;

                IDisposable s1 = first.Subscribe(new AnonymousObserver<TFirst>(
                    value =>
                    {
                        TResult result = default!;
                        bool emit;
                        Exception? error = null;
                        lock (gate)
                        {
                            v1 = value;
                            has1 = true;
                            emit = has2;
                            // An exception from the combiner is delivered to the subscriber as OnError rather than propagated
                            // into the code that changed the source. OnError is delivered outside the lock.
                            if (emit)
                            {
                                try { result = resultSelector(v1, v2); }
                                catch (Exception ex) { error = ex; emit = false; }
                            }
                        }
                        if (error is not null) { observer.OnError(error); return; }
                        if (emit) observer.OnNext(result);
                    },
                    observer.OnError,
                    () =>
                    {
                        bool complete;
                        lock (gate) { done1 = true; complete = done2; }
                        if (complete) observer.OnCompleted();
                    }));

                IDisposable s2 = second.Subscribe(new AnonymousObserver<TSecond>(
                    value =>
                    {
                        TResult result = default!;
                        bool emit;
                        Exception? error = null;
                        lock (gate)
                        {
                            v2 = value;
                            has2 = true;
                            emit = has1;
                            if (emit)
                            {
                                try { result = resultSelector(v1, v2); }
                                catch (Exception ex) { error = ex; emit = false; }
                            }
                        }
                        if (error is not null) { observer.OnError(error); return; }
                        if (emit) observer.OnNext(result);
                    },
                    observer.OnError,
                    () =>
                    {
                        bool complete;
                        lock (gate) { done2 = true; complete = done1; }
                        if (complete) observer.OnCompleted();
                    }));

                return new CompositeDisposable(s1, s2);
            });

        /// <summary>
        /// Switches to the latest inner sequence, unsubscribing from the previous one.
        /// </summary>
        public static IObservable<T> Switch<T>(this IObservable<IObservable<T>> sources)
            => new AnonymousObservable<T>(observer =>
            {
                object gate = new();
                SerialDisposable inner = new();
                bool outerCompleted = false;
                bool hasInner = false;
                ulong latest = 0;

                IDisposable outer = sources.Subscribe(new AnonymousObserver<IObservable<T>>(
                    source =>
                    {
                        ulong id;
                        lock (gate)
                        {
                            hasInner = true;
                            latest++;
                            id = latest;
                        }

                        SingleAssignmentDisposable subscription = new();
                        inner.Disposable = subscription;
                        subscription.Disposable = source.Subscribe(new AnonymousObserver<T>(
                            value =>
                            {
                                lock (gate)
                                {
                                    if (id != latest)
                                        return;
                                }
                                observer.OnNext(value);
                            },
                            observer.OnError,
                            () =>
                            {
                                bool complete = false;
                                lock (gate)
                                {
                                    if (id == latest)
                                    {
                                        hasInner = false;
                                        complete = outerCompleted;
                                    }
                                }
                                if (complete) observer.OnCompleted();
                            }));
                    },
                    observer.OnError,
                    () =>
                    {
                        bool complete;
                        lock (gate)
                        {
                            outerCompleted = true;
                            complete = !hasInner;
                        }
                        if (complete) observer.OnCompleted();
                    }));

                return new CompositeDisposable(outer, inner);
            });

        #endregion

        #region Scheduler operators

        /// <summary>
        /// Emits a value only after a quiet period of dueTime (debounce). The scheduler is the UI thread.
        /// </summary>
        public static IObservable<T> Throttle<T>(this IObservable<T> source, TimeSpan dueTime)
            => Throttle(source, dueTime, RxSchedulers.MainThreadScheduler);

        /// <summary>
        /// Emits a value only after a quiet period of dueTime (debounce) on the specified scheduler.
        /// </summary>
        public static IObservable<T> Throttle<T>(this IObservable<T> source, TimeSpan dueTime, IScheduler scheduler)
            => new AnonymousObservable<T>(observer =>
            {
                object gate = new();
                SerialDisposable cancelable = new();
                bool hasValue = false;
                T value = default!;
                ulong id = 0;

                IDisposable subscription = source.Subscribe(new AnonymousObserver<T>(
                    next =>
                    {
                        ulong currentId;
                        lock (gate)
                        {
                            hasValue = true;
                            value = next;
                            currentId = ++id;
                        }

                        cancelable.Disposable = scheduler.Schedule(dueTime, () =>
                        {
                            T toEmit = default!;
                            bool emit = false;
                            lock (gate)
                            {
                                if (hasValue && id == currentId)
                                {
                                    emit = true;
                                    toEmit = value;
                                    hasValue = false;
                                }
                            }
                            if (emit) observer.OnNext(toEmit);
                        });
                    },
                    ex =>
                    {
                        cancelable.Dispose();
                        observer.OnError(ex);
                    },
                    () =>
                    {
                        cancelable.Dispose();
                        T toEmit = default!;
                        bool emit;
                        lock (gate)
                        {
                            emit = hasValue;
                            if (emit) toEmit = value;
                            hasValue = false;
                        }
                        if (emit) observer.OnNext(toEmit);
                        observer.OnCompleted();
                    }));

                return new CompositeDisposable(subscription, cancelable);
            });

        /// <summary>
        /// Moves value emission to the specified scheduler (usually the UI thread).
        /// </summary>
        public static IObservable<T> ObserveOn<T>(this IObservable<T> source, IScheduler scheduler)
            => new AnonymousObservable<T>(observer =>
            {
                // Notifications accumulate in a queue and the scheduler receives a pass over it, so the delivery order
                // does not depend on the scheduler. Without the queue an inline scheduler would run a
                // value from the UI thread immediately, ahead of an older one still waiting in the dispatcher queue.
                object gate = new();
                Queue<Action> queue = new();
                bool draining = false;

                // An already scheduled pass cannot be cancelled via its token. So on unsubscribe
                // the queue is drained by a shared flag, and the observer receives nothing after the subscription is disposed.
                BooleanDisposable stopped = new();

                void Drain()
                {
                    while (true)
                    {
                        Action next;
                        lock (gate)
                        {
                            if (stopped.IsDisposed)
                                queue.Clear();

                            if (queue.Count == 0)
                            {
                                draining = false;
                                return;
                            }

                            next = queue.Dequeue();
                        }

                        bool delivered = false;
                        try
                        {
                            next();
                            delivered = true;
                        }
                        finally
                        {
                            // A subscriber exception goes to the scheduler, and the rest of the queue is delivered by a new pass.
                            if (!delivered)
                                scheduler.Schedule(Drain);
                        }
                    }
                }

                void Enqueue(Action delivery)
                {
                    bool schedule;
                    lock (gate)
                    {
                        queue.Enqueue(delivery);
                        schedule = !draining;
                        draining = true;
                    }

                    if (schedule)
                        scheduler.Schedule(Drain);
                }

                IDisposable subscription = source.Subscribe(new AnonymousObserver<T>(
                    value => Enqueue(() => observer.OnNext(value)),
                    ex => Enqueue(() => observer.OnError(ex)),
                    () => Enqueue(observer.OnCompleted)));

                return new CompositeDisposable(subscription, stopped);
            });

        #endregion
    }
}
