using AvaloniaLight.Reactive.Linq;

namespace AvaloniaLight.Reactive.Tests;

/// <summary>
/// Tests for the operators and factories of AvaloniaLight.Reactive.Linq: filtering, completion, and the Rx contract
/// (after OnError or OnCompleted the subscriber receives nothing, and sources are released).
/// The tests are pure: time and the queue are driven by a manual scheduler.
/// </summary>
public class OperatorTests
{
    /// <summary>
    /// Manual scheduler: actions run only when time advances, at their due time and in the order they were queued.
    /// </summary>
    private sealed class ManualScheduler : IScheduler
    {
        private readonly List<(TimeSpan Due, Action Action, BooleanDisposable Cancel)> _queue = new();
        private TimeSpan _now;

        public int Pending => _queue.Count(item => !item.Cancel.IsDisposed);

        public IDisposable Schedule(Action action) => Schedule(TimeSpan.Zero, action);

        public IDisposable Schedule(TimeSpan dueTime, Action action)
        {
            BooleanDisposable cancel = new();
            _queue.Add((_now + (dueTime > TimeSpan.Zero ? dueTime : TimeSpan.Zero), action, cancel));
            return cancel;
        }

        public void AdvanceBy(TimeSpan time)
        {
            TimeSpan target = _now + time;
            while (true)
            {
                int index = -1;
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (_queue[i].Due <= target && (index < 0 || _queue[i].Due < _queue[index].Due))
                        index = i;
                }

                if (index < 0)
                    break;

                (TimeSpan due, Action action, BooleanDisposable cancel) = _queue[index];
                _queue.RemoveAt(index);
                _now = due;
                if (!cancel.IsDisposed)
                    action();
            }

            _now = target;
        }

        public void Run() => AdvanceBy(TimeSpan.Zero);
    }

    /// <summary>
    /// Subscriber notification log: values, errors, and completions in arrival order.
    /// </summary>
    private sealed class Recorder<T> : IObserver<T>
    {
        public List<string> Events { get; } = new();

        public void OnNext(T value) => Events.Add($"next:{value}");

        public void OnError(Exception error) => Events.Add("error");

        public void OnCompleted() => Events.Add("completed");
    }

    #region Simple operators

    [Fact]
    public void Where_Predicate_PassesOnlyMatching()
    {
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.Where(x => x % 2 == 0).Subscribe(recorder);

        for (int i = 1; i <= 4; i++)
            subject.OnNext(i);

        Assert.Equal(new[] { "next:2", "next:4" }, recorder.Events);
    }

    [Fact]
    public void Where_SyncSourcePredicateThrows_NothingAfterError()
    {
        // The source emits everything synchronously on subscribe: the error occurs before Where received the subscription.
        IObservable<int> source = Observable.Create<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnNext(2);
            observer.OnCompleted();
            return Disposable.Empty;
        });
        Recorder<int> recorder = new();

        using IDisposable _ = source.Where(x => x == 1 ? throw new InvalidOperationException() : true).Subscribe(recorder);

        Assert.Equal(new[] { "error" }, recorder.Events);
    }

    [Fact]
    public void Select_Selector_ProjectsValues()
    {
        Subject<int> subject = new();
        Recorder<string> recorder = new();
        using IDisposable _ = subject.Select(x => $"v{x}").Subscribe(recorder);

        subject.OnNext(1);
        subject.OnCompleted();

        Assert.Equal(new[] { "next:v1", "completed" }, recorder.Events);
    }

    [Fact]
    public void Select_SyncCompletingSourceSelectorThrows_NoCompletionAfterError()
    {
        Recorder<int> recorder = new();

        using IDisposable _ = Observable.Return(1).Select<int, int>(_ => throw new InvalidOperationException()).Subscribe(recorder);

        Assert.Equal(new[] { "error" }, recorder.Events);
    }

    [Fact]
    public void Do_ActionThrows_SendsOnErrorOnceAndDetaches()
    {
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        int calls = 0;
        using IDisposable _ = subject.Do(_ => { calls++; throw new InvalidOperationException(); }).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnNext(2);

        Assert.Equal(1, calls);
        Assert.Equal(new[] { "error" }, recorder.Events);
        Assert.False(subject.HasObservers);
    }

    [Fact]
    public void Skip_Count_SkipsFirstValues()
    {
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.Skip(2).Subscribe(recorder);

        for (int i = 1; i <= 4; i++)
            subject.OnNext(i);

        Assert.Equal(new[] { "next:3", "next:4" }, recorder.Events);
    }

    [Fact]
    public void Take_HotSource_CompletesAndDetaches()
    {
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.Take(2).Subscribe(recorder);

        for (int i = 1; i <= 3; i++)
            subject.OnNext(i);

        Assert.Equal(new[] { "next:1", "next:2", "completed" }, recorder.Events);
        Assert.False(subject.HasObservers);
    }

    [Fact]
    public void Take_SyncCompletingSource_CompletesOnce()
    {
        Recorder<int> recorder = new();

        using IDisposable _ = Observable.Return(1).Take(1).Subscribe(recorder);

        Assert.Equal(new[] { "next:1", "completed" }, recorder.Events);
    }

    [Fact]
    public void Take_Zero_CompletesImmediately()
    {
        Subject<int> subject = new();
        Recorder<int> recorder = new();

        using IDisposable _ = subject.Take(0).Subscribe(recorder);

        Assert.Equal(new[] { "completed" }, recorder.Events);
        Assert.False(subject.HasObservers);
    }

    [Fact]
    public void DistinctUntilChanged_Repeats_SkipsConsecutiveDuplicates()
    {
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.DistinctUntilChanged().Subscribe(recorder);

        foreach (int value in new[] { 1, 1, 2, 2, 1 })
            subject.OnNext(value);

        Assert.Equal(new[] { "next:1", "next:2", "next:1" }, recorder.Events);
    }

    [Fact]
    public void DistinctUntilChanged_Comparer_UsesComparer()
    {
        Subject<string> subject = new();
        Recorder<string> recorder = new();
        using IDisposable _ = subject.DistinctUntilChanged(StringComparer.OrdinalIgnoreCase).Subscribe(recorder);

        subject.OnNext("a");
        subject.OnNext("A");
        subject.OnNext("b");

        Assert.Equal(new[] { "next:a", "next:b" }, recorder.Events);
    }

    [Fact]
    public void StartWith_Value_EmitsBeforeSource()
    {
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.StartWith(0).Subscribe(recorder);

        subject.OnNext(1);

        Assert.Equal(new[] { "next:0", "next:1" }, recorder.Events);
    }

    #endregion

    #region Combining operators

    [Fact]
    public void Merge_BothSources_CompletesAfterBoth()
    {
        Subject<int> first = new();
        Subject<int> second = new();
        Recorder<int> recorder = new();
        using IDisposable _ = first.Merge(second).Subscribe(recorder);

        first.OnNext(1);
        second.OnNext(2);
        first.OnCompleted();
        second.OnNext(3);
        second.OnCompleted();

        Assert.Equal(new[] { "next:1", "next:2", "next:3", "completed" }, recorder.Events);
    }

    [Fact]
    public void Merge_ErrorInOneSource_DetachesOther()
    {
        Subject<int> first = new();
        Subject<int> second = new();
        Recorder<int> recorder = new();
        using IDisposable _ = first.Merge(second).Subscribe(recorder);

        first.OnError(new InvalidOperationException());
        second.OnNext(5);
        second.OnError(new InvalidOperationException());

        Assert.Equal(new[] { "error" }, recorder.Events);
        Assert.False(second.HasObservers);
    }

    [Fact]
    public void CombineLatest_BothHaveValues_CombinesLatest()
    {
        Subject<int> first = new();
        Subject<int> second = new();
        Recorder<int> recorder = new();
        using IDisposable _ = first.CombineLatest(second, (a, b) => a * 10 + b).Subscribe(recorder);

        first.OnNext(1);
        second.OnNext(2);
        first.OnNext(3);

        Assert.Equal(new[] { "next:12", "next:32" }, recorder.Events);
    }

    [Fact]
    public void CombineLatest_SelectorThrows_SendsOnErrorOnceAndDetaches()
    {
        Subject<int> first = new();
        Subject<int> second = new();
        Recorder<int> recorder = new();
        using IDisposable _ = first.CombineLatest(second, (a, b) => 10 / (a + b)).Subscribe(recorder);

        first.OnNext(0);
        second.OnNext(0);
        second.OnNext(1);

        Assert.Equal(new[] { "error" }, recorder.Events);
        Assert.False(first.HasObservers);
        Assert.False(second.HasObservers);
    }

    [Fact]
    public void Switch_NewInner_DetachesPrevious()
    {
        Subject<IObservable<int>> outer = new();
        Subject<int> inner1 = new();
        Subject<int> inner2 = new();
        Recorder<int> recorder = new();
        using IDisposable _ = outer.Switch().Subscribe(recorder);

        outer.OnNext(inner1);
        inner1.OnNext(1);
        outer.OnNext(inner2);
        inner1.OnNext(2);
        inner2.OnNext(3);

        Assert.Equal(new[] { "next:1", "next:3" }, recorder.Events);
        Assert.False(inner1.HasObservers);
    }

    [Fact]
    public void Switch_InnerError_DetachesOuter()
    {
        Subject<IObservable<int>> outer = new();
        Subject<int> inner = new();
        Recorder<int> recorder = new();
        using IDisposable _ = outer.Switch().Subscribe(recorder);

        outer.OnNext(inner);
        inner.OnError(new InvalidOperationException());
        outer.OnNext(Observable.Return(3));

        Assert.Equal(new[] { "error" }, recorder.Events);
        Assert.False(outer.HasObservers);
    }

    [Fact]
    public void Switch_OuterCompletedAfterInner_Completes()
    {
        Subject<IObservable<int>> outer = new();
        Subject<int> inner = new();
        Recorder<int> recorder = new();
        using IDisposable _ = outer.Switch().Subscribe(recorder);

        outer.OnNext(inner);
        outer.OnCompleted();
        inner.OnNext(1);
        inner.OnCompleted();

        Assert.Equal(new[] { "next:1", "completed" }, recorder.Events);
    }

    #endregion

    #region Scheduler operators

    [Fact]
    public void Throttle_BurstOfValues_EmitsLastAfterPause()
    {
        ManualScheduler scheduler = new();
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.Throttle(TimeSpan.FromSeconds(1), scheduler).Subscribe(recorder);

        subject.OnNext(1);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500));
        subject.OnNext(2);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500));
        Assert.Empty(recorder.Events);

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500));
        Assert.Equal(new[] { "next:2" }, recorder.Events);
    }

    [Fact]
    public void Throttle_Completed_FlushesPendingValue()
    {
        ManualScheduler scheduler = new();
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.Throttle(TimeSpan.FromSeconds(1), scheduler).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnCompleted();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2));

        Assert.Equal(new[] { "next:1", "completed" }, recorder.Events);
    }

    [Fact]
    public void ObserveOn_SeveralNotifications_DeliversInOrderWithOnePass()
    {
        ManualScheduler scheduler = new();
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        using IDisposable _ = subject.ObserveOn(scheduler).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnCompleted();
        Assert.Equal(1, scheduler.Pending);
        Assert.Empty(recorder.Events);

        scheduler.Run();

        Assert.Equal(new[] { "next:1", "next:2", "completed" }, recorder.Events);
    }

    [Fact]
    public void ObserveOn_DisposedBeforeDelivery_DeliversNothing()
    {
        ManualScheduler scheduler = new();
        Subject<int> subject = new();
        Recorder<int> recorder = new();
        IDisposable subscription = subject.ObserveOn(scheduler).Subscribe(recorder);

        subject.OnNext(1);
        subscription.Dispose();
        scheduler.Run();

        Assert.Empty(recorder.Events);
    }

    #endregion

    #region Factories

    [Fact]
    public void Create_NotificationsAfterError_Ignored()
    {
        Recorder<int> recorder = new();
        bool disposed = false;
        IObservable<int> source = Observable.Create<int>(observer =>
        {
            observer.OnError(new InvalidOperationException());
            observer.OnNext(1);
            observer.OnCompleted();
            return Disposable.Create(() => disposed = true);
        });

        using IDisposable _ = source.Subscribe(recorder);

        Assert.Equal(new[] { "error" }, recorder.Events);
        Assert.True(disposed);
    }

    [Fact]
    public void ReturnEmptyNever_Subscribe_EmitExpectedNotifications()
    {
        Recorder<int> returned = new();
        Recorder<int> empty = new();
        Recorder<int> never = new();

        using IDisposable a = Observable.Return(7).Subscribe(returned);
        using IDisposable b = Observable.Empty<int>().Subscribe(empty);
        using IDisposable c = Observable.Never<int>().Subscribe(never);

        Assert.Equal(new[] { "next:7", "completed" }, returned.Events);
        Assert.Equal(new[] { "completed" }, empty.Events);
        Assert.Empty(never.Events);
    }

    [Fact]
    public void FromEventPattern_Dispose_RemovesHandler()
    {
        EventHandler? handlers = null;
        List<object?> senders = new();
        IDisposable subscription = Observable.FromEventPattern(h => handlers += h, h => handlers -= h)
            .Subscribe(new AnonymousObserver<EventPattern<EventArgs>>(e => senders.Add(e.Sender)));

        handlers?.Invoke("first", EventArgs.Empty);
        subscription.Dispose();

        Assert.Null(handlers);
        Assert.Equal(new object?[] { "first" }, senders);
    }

    #endregion
}
