namespace LightReactive.Avalonia
{
    /// <summary>
    /// Pairs an event sender with its event arguments. Replacement for System.Reactive.EventPattern.
    /// </summary>
    public sealed class EventPattern<TEventArgs>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public EventPattern(object? sender, TEventArgs eventArgs)
        {
            Sender = sender;
            EventArgs = eventArgs;
        }

        /// <summary>
        /// The event sender.
        /// </summary>
        public object? Sender { get; }

        /// <summary>
        /// The event arguments.
        /// </summary>
        public TEventArgs EventArgs { get; }
    }
}

namespace LightReactive.Avalonia.Linq
{
    /// <summary>
    /// Factory for observable sequences. Replacement for System.Reactive.Linq.Observable (the subset in use).
    /// Lives in the .Linq namespace to avoid conflicts with System.Reactive.Linq.Observable while both coexist.
    /// </summary>
    public static class Observable
    {
        /// <summary>
        /// A sequence of a single value, emitted immediately on subscription.
        /// </summary>
        public static IObservable<T> Return<T>(T value)
            => new AnonymousObservable<T>(observer =>
            {
                observer.OnNext(value);
                observer.OnCompleted();
                return Disposable.Empty;
            });

        /// <summary>
        /// An empty sequence (completes immediately).
        /// </summary>
        public static IObservable<T> Empty<T>()
            => new AnonymousObservable<T>(observer =>
            {
                observer.OnCompleted();
                return Disposable.Empty;
            });

        /// <summary>
        /// A sequence that never emits anything and never completes.
        /// </summary>
        public static IObservable<T> Never<T>()
            => new AnonymousObservable<T>(_ => Disposable.Empty);

        /// <summary>
        /// Creates an observable from a subscribe delegate.
        /// </summary>
        public static IObservable<T> Create<T>(Func<IObserver<T>, IDisposable> subscribe)
            => new AnonymousObservable<T>(subscribe);

        /// <summary>
        /// Converts an <see cref="EventHandler"/> event into an observable sequence.
        /// </summary>
        public static IObservable<EventPattern<EventArgs>> FromEventPattern(
            Action<EventHandler> addHandler,
            Action<EventHandler> removeHandler)
            => new AnonymousObservable<EventPattern<EventArgs>>(observer =>
            {
                void Handler(object? sender, EventArgs e) => observer.OnNext(new EventPattern<EventArgs>(sender, e));
                addHandler(Handler);
                return Disposable.Create(() => removeHandler(Handler));
            });
    }
}
