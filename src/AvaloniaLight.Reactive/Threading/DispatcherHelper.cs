using Avalonia.Threading;

namespace AvaloniaLight.Reactive.Threading
{
    /// <summary>
    /// Helper methods for working with the UI thread dispatcher.
    /// </summary>
    public static class DispatcherHelper
    {
        #region Invoke

        /// <summary>
        /// Returns the dispatcher priority, taking the default value into account.
        /// </summary>
        private static DispatcherPriority GetDispatcherPriority(DispatcherPriority? priority = null)
        {
            return priority ?? DispatcherPriority.Normal;
        }

        /// <summary>
        /// Determines whether the current code is running on the UI thread.
        /// </summary>
        public static bool CheckAccess()
        {
            return Dispatcher.UIThread.CheckAccess();
        }

        /// <summary>
        /// Asynchronously posts the action to the dispatcher queue.
        /// </summary>
        public static void BeginInvoke(Action action, DispatcherPriority? priority = null)
        {
            Dispatcher.UIThread.Post(action, GetDispatcherPriority(priority));
        }

        /// <summary>
        /// Synchronously runs the action on the UI thread.
        /// </summary>
        public static void Invoke(Action action, DispatcherPriority? priority = null)
        {
            if (CheckAccess())
                action();
            else
                Dispatcher.UIThread.Invoke(action, GetDispatcherPriority(priority));
        }

        /// <summary>
        /// Asynchronously runs the action on the UI thread.
        /// </summary>
        public static Task InvokeAsync(Action action, DispatcherPriority? priority = null)
        {
            if (CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return Dispatcher.UIThread.InvokeAsync(action, GetDispatcherPriority(priority)).GetTask();
        }

        /// <summary>
        /// Asynchronously runs a function with a result on the UI thread.
        /// </summary>
        public static Task<TResult> InvokeAsync<TResult>(Func<TResult> function, DispatcherPriority? priority = null)
        {
            if (CheckAccess())
                return Task.FromResult(function());

            return Dispatcher.UIThread.InvokeAsync(function, GetDispatcherPriority(priority)).GetTask();
        }

        /// <summary>
        /// Asynchronously runs an asynchronous function on the UI thread.
        /// </summary>
        public static Task InvokeAsync(Func<Task> function, DispatcherPriority? priority = null)
        {
            if (CheckAccess())
                return function();

            return Dispatcher.UIThread.InvokeAsync(function, GetDispatcherPriority(priority));
        }

        /// <summary>
        /// Asynchronously runs an asynchronous function with a result on the UI thread.
        /// </summary>
        public static Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> function, DispatcherPriority? priority = null)
        {
            if (CheckAccess())
                return function();

            return Dispatcher.UIThread.InvokeAsync(function, GetDispatcherPriority(priority));
        }

        #endregion

        #region Timers

        /// <summary>
        /// Starts a timer with an interval in milliseconds.
        /// </summary>
        public static IDisposable RunTimer(Action action, double milliseconds, DispatcherPriority? priority = null)
        {
            var func = () =>
            {
                action.Invoke();
                return true;
            };

            return RunTimer(func, TimeSpan.FromMilliseconds(milliseconds), GetDispatcherPriority(priority));
        }

        /// <summary>
        /// Starts a periodic timer with a continuation function. The timer runs on the UI thread.
        /// </summary>
        public static IDisposable RunTimer(Func<bool> action, TimeSpan interval, DispatcherPriority? priority = null)
        {
            DispatcherPriority dispatcherPriority = GetDispatcherPriority(priority);
            return RunOnUiThread(() => DispatcherTimer.Run(action, interval, dispatcherPriority));
        }

        /// <summary>
        /// Starts a one-shot timer. The timer runs on the UI thread.
        /// </summary>
        public static IDisposable RunTimerOnce(Action action, TimeSpan interval, DispatcherPriority? priority = null)
        {
            DispatcherPriority dispatcherPriority = GetDispatcherPriority(priority);
            return RunOnUiThread(() => DispatcherTimer.RunOnce(action, interval, dispatcherPriority));
        }

        /// <summary>
        /// Creates a timer on the UI thread. DispatcherTimer.Run and RunOnce bind the timer to the dispatcher
        /// of the current thread: on a background thread that is a dispatcher without a loop, and the timer would never fire.
        /// </summary>
        private static IDisposable RunOnUiThread(Func<IDisposable> start)
        {
            if (CheckAccess())
                return start();

            SerialDisposable timer = new();
            Dispatcher.UIThread.Post(() =>
            {
                if (!timer.IsDisposed)
                    timer.Disposable = start();
            });
            return timer;
        }

        #endregion

        #region SynchronizationContext

        /// <summary>
        /// Temporarily installs a new SynchronizationContext with automatic restoration.
        /// </summary>
        public static IDisposable UseNewSynchronizationContext(SynchronizationContext? newContext = null)
        {
            return new RestoreSynchronizationContext(
                newContext ?? new SynchronizationContext(),
                SynchronizationContext.Current);
        }

        #endregion
    }

    /// <summary>
    /// Structure for automatically restoring the SynchronizationContext.
    /// </summary>
    public record struct RestoreSynchronizationContext : IDisposable
    {
        private readonly SynchronizationContext? _oldContext;

        private bool _needRestore;

        /// <summary>
        /// Creates a context, immediately installing a new SynchronizationContext.
        /// </summary>
        public RestoreSynchronizationContext(
            SynchronizationContext newContext, SynchronizationContext? oldContext)
            : this(oldContext)
        {
            SynchronizationContext.SetSynchronizationContext(newContext);
        }

        /// <summary>
        /// Creates a context, saving the current SynchronizationContext.
        /// </summary>
        public RestoreSynchronizationContext(SynchronizationContext? oldContext)
        {
            _oldContext = oldContext;
            _needRestore = true;
        }

        /// <summary>
        /// Restores the original SynchronizationContext.
        /// </summary>
        public void Dispose()
        {
            if (_needRestore)
            {
                SynchronizationContext.SetSynchronizationContext(_oldContext);
                _needRestore = false;
            }
        }
    }
}