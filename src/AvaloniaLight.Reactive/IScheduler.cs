using Avalonia.Threading;

namespace AvaloniaLight.Reactive
{
    /// <summary>
    /// Scheduler for executing actions. Simplified replacement for System.Reactive.Concurrency.IScheduler.
    /// Sufficient for the ObserveOn and Throttle operators.
    /// </summary>
    public interface IScheduler
    {
        /// <summary>
        /// Schedules the action to run as soon as possible. Returns a cancellation token.
        /// </summary>
        IDisposable Schedule(Action action);

        /// <summary>
        /// Schedules the action to run after a delay. Returns a cancellation token.
        /// </summary>
        IDisposable Schedule(TimeSpan dueTime, Action action);
    }

    /// <summary>
    /// Scheduler that runs actions on the Avalonia UI thread.
    /// </summary>
    public sealed class AvaloniaMainThreadScheduler : IScheduler
    {
        /// <summary>
        /// The single instance.
        /// </summary>
        public static readonly AvaloniaMainThreadScheduler Instance = new();

        private AvaloniaMainThreadScheduler()
        {
        }

        /// <inheritdoc />
        public IDisposable Schedule(Action action) => Schedule(TimeSpan.Zero, action);

        /// <inheritdoc />
        public IDisposable Schedule(TimeSpan dueTime, Action action)
        {
            if (dueTime <= TimeSpan.Zero)
            {
                // Default priority: the action goes into the shared queue rather than behind everything (Background),
                // as the method description "as soon as possible" promises.
                BooleanDisposable cancel = new();
                Dispatcher.UIThread.Post(() =>
                {
                    if (!cancel.IsDisposed)
                        action();
                }, DispatcherPriority.Default);
                return cancel;
            }

            // The dispatcher timer is created on the UI thread. RunOnce creates the timer itself, waits for the interval,
            // invokes the action and stops the timer; returns a cancellation token.
            SerialDisposable serial = new();
            Dispatcher.UIThread.Post(() =>
            {
                if (serial.IsDisposed)
                    return;

                serial.Disposable = DispatcherTimer.RunOnce(action, dueTime, DispatcherPriority.Default);
            });
            return serial;
        }
    }

    /// <summary>
    /// Avalonia UI thread scheduler with the behavior of RxApp.MainThreadScheduler from Avalonia.ReactiveUI:
    /// an action without delay scheduled from the UI thread runs immediately, within the same call.
    /// From other threads, and with a delay, the action goes through <see cref="AvaloniaMainThreadScheduler"/>.
    /// </summary>
    public sealed class AvaloniaMainThreadInlineScheduler : IScheduler
    {
        // As in Avalonia.ReactiveUI: the depth of nested calls is limited so that chains of
        // subscriptions that schedule actions for each other do not overflow the stack.
        private const int MaxReentrancy = 32;

        [ThreadStatic]
        private static int _reentrancy;

        /// <summary>
        /// The single instance.
        /// </summary>
        public static readonly AvaloniaMainThreadInlineScheduler Instance = new();

        private AvaloniaMainThreadInlineScheduler()
        {
        }

        /// <inheritdoc />
        public IDisposable Schedule(Action action) => Schedule(TimeSpan.Zero, action);

        /// <inheritdoc />
        public IDisposable Schedule(TimeSpan dueTime, Action action)
        {
            if (dueTime <= TimeSpan.Zero && _reentrancy < MaxReentrancy && Dispatcher.UIThread.CheckAccess())
            {
                _reentrancy++;
                try
                {
                    action();
                }
                finally
                {
                    _reentrancy--;
                }

                return Disposable.Empty;
            }

            return AvaloniaMainThreadScheduler.Instance.Schedule(dueTime, action);
        }
    }

    /// <summary>
    /// Access to schedulers. Replacement for the static scheduler helper from ReactiveUI.
    /// </summary>
    public static class RxSchedulers
    {
        /// <summary>
        /// UI thread scheduler. Always enqueues the action on the dispatcher,
        /// even if it is scheduled from the UI thread.
        /// </summary>
        public static IScheduler MainThreadScheduler => AvaloniaMainThreadScheduler.Instance;

        /// <summary>
        /// UI thread scheduler that runs an action without delay immediately if it is scheduled
        /// from the UI thread. This is how RxApp.MainThreadScheduler worked in Avalonia.ReactiveUI.
        /// </summary>
        public static IScheduler MainThreadInlineScheduler => AvaloniaMainThreadInlineScheduler.Instance;
    }
}
