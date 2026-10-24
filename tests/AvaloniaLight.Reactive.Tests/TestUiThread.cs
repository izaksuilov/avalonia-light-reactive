using Avalonia.Threading;

namespace AvaloniaLight.Reactive.Tests;

/// <summary>
/// Avalonia UI thread for scheduler tests. In Avalonia 12 the UI thread is the thread
/// that first accesses Dispatcher.UIThread. So the thread is created up front and runs
/// the dispatcher loop, and tests touch the dispatcher only after it has started.
/// </summary>
internal static class TestUiThread
{
    private static readonly Lazy<Dispatcher> Instance = new(Start);

    /// <summary>
    /// Starts the UI thread if it is not already running.
    /// </summary>
    public static void EnsureStarted() => _ = Instance.Value;

    /// <summary>
    /// Runs the action on the UI thread. An exception thrown by the action is propagated through the task.
    /// </summary>
    public static Task InvokeAsync(Action action) => Instance.Value.InvokeAsync(action).GetTask();

    private static Dispatcher Start()
    {
        TaskCompletionSource<Dispatcher> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            Dispatcher dispatcher = Dispatcher.UIThread;
            if (!dispatcher.CheckAccess())
            {
                ready.SetException(new InvalidOperationException("The UI thread dispatcher has already been created on another thread."));
                return;
            }

            ready.SetResult(dispatcher);
            dispatcher.MainLoop(CancellationToken.None);
        })
        {
            IsBackground = true,
            Name = "Avalonia test UI thread",
        };
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }
}
