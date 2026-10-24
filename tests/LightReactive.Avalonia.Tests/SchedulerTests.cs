using Avalonia.Threading;
using LightReactive.Avalonia.Linq;
using LightReactive.Avalonia.Threading;

namespace LightReactive.Avalonia.Tests;

/// <summary>
/// Tests for UI-thread schedulers: <see cref="RxSchedulers.MainThreadScheduler"/> always queues the action
/// on the dispatcher queue, <see cref="RxSchedulers.MainThreadInlineScheduler"/> runs it immediately when called from the UI thread.
/// </summary>
public class SchedulerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public SchedulerTests() => TestUiThread.EnsureStarted();

    [Fact]
    public async Task MainThreadScheduler_FromUiThread_RunsAfterCurrentCall()
    {
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool ranInline = true;

        await TestUiThread.InvokeAsync(() =>
        {
            bool ran = false;
            RxSchedulers.MainThreadScheduler.Schedule(() =>
            {
                ran = true;
                done.SetResult();
            });
            ranInline = ran;
        });

        await done.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.False(ranInline);
    }

    [Fact]
    public Task InlineScheduler_FromUiThread_RunsInSameCall()
        => TestUiThread.InvokeAsync(() =>
        {
            bool ran = false;
            RxSchedulers.MainThreadInlineScheduler.Schedule(() => ran = true);
            Assert.True(ran);
        });

    [Fact]
    public async Task InlineScheduler_FromOtherThread_RunsOnUiThread()
    {
        TaskCompletionSource<bool> onUiThread = new(TaskCreationOptions.RunContinuationsAsynchronously);

        RxSchedulers.MainThreadInlineScheduler.Schedule(() => onUiThread.SetResult(Dispatcher.UIThread.CheckAccess()));

        Assert.True(await onUiThread.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InlineScheduler_WithDelay_RunsAfterCurrentCall()
    {
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool ranInline = true;

        await TestUiThread.InvokeAsync(() =>
        {
            bool ran = false;
            RxSchedulers.MainThreadInlineScheduler.Schedule(TimeSpan.FromMilliseconds(1), () =>
            {
                ran = true;
                done.SetResult();
            });
            ranInline = ran;
        });

        await done.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.False(ranInline);
    }

    [Fact]
    public async Task InlineScheduler_DeepNesting_FallsBackToQueue()
    {
        const int total = 40;
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int depth = 0, maxDepth = 0, calls = 0;

        void Step()
        {
            depth++;
            maxDepth = Math.Max(maxDepth, depth);
            calls++;
            if (calls < total)
                RxSchedulers.MainThreadInlineScheduler.Schedule(Step);
            else
                done.SetResult();
            depth--;
        }

        await TestUiThread.InvokeAsync(Step);
        await done.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        // The direct call and 32 nested ones run immediately; the rest arrive through the dispatcher queue.
        Assert.Equal(33, maxDepth);
        Assert.Equal(total, calls);
    }

    [Fact]
    public Task ObserveOn_InlineScheduler_FromUiThread_DeliversInSameCall()
        => TestUiThread.InvokeAsync(() =>
        {
            Subject<int> subject = new();
            List<int> received = new();
            using IDisposable _ = subject.ObserveOn(RxSchedulers.MainThreadInlineScheduler)
                .Subscribe(new AnonymousObserver<int>(received.Add));

            subject.OnNext(1);

            Assert.Equal(new[] { 1 }, received);
        });

    [Fact]
    public async Task ObserveOn_InlineScheduler_BackgroundValueThenUiValue_KeepsOrder()
    {
        Subject<int> subject = new();
        List<int> received = new();
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable _ = subject.ObserveOn(RxSchedulers.MainThreadInlineScheduler)
            .Subscribe(new AnonymousObserver<int>(value =>
            {
                received.Add(value);
                if (received.Count == 2)
                    done.SetResult();
            }));

        await TestUiThread.InvokeAsync(() =>
        {
            // A value from a background thread waits in the dispatcher queue while the UI thread is busy.
            // The next value from the UI thread must not overtake it.
            Task.Run(() => subject.OnNext(1)).Wait(TestContext.Current.CancellationToken);
            subject.OnNext(2);
        });

        await done.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 1, 2 }, received);
    }

    [Fact]
    public async Task DispatcherHelper_RunTimerOnce_FromBackgroundThread_FiresOnUiThread()
    {
        TaskCompletionSource<bool> fired = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable timer = await Task.Run(() => DispatcherHelper.RunTimerOnce(
            () => fired.TrySetResult(Dispatcher.UIThread.CheckAccess()), TimeSpan.FromMilliseconds(1)));

        Assert.True(await fired.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatcherHelper_RunTimer_FromBackgroundThread_TicksUntilFalse()
    {
        int ticks = 0;
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable timer = await Task.Run(() => DispatcherHelper.RunTimer(() =>
        {
            if (++ticks < 3)
                return true;

            done.TrySetResult();
            return false;
        }, TimeSpan.FromMilliseconds(1)));

        await done.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(3, ticks);
    }
}
