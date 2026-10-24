using AvaloniaLight.Reactive.Linq;

namespace AvaloniaLight.Reactive.Tests;

/// <summary>
/// Tests for the core of the in-house reactive library (a replacement for ReactiveUI/System.Reactive):
/// subjects, the subscription container, property observation, and operator error handling.
/// The tests are pure (no Avalonia UI thread).
/// </summary>
public class ReactiveCoreTests
{
    private sealed class TestVm : ReactiveObject
    {
        private string? _name;
        public string? Name { get => _name; set => RaiseAndSetIfChanged(ref _name, value); }

        private string? _other;
        public string? Other { get => _other; set => RaiseAndSetIfChanged(ref _other, value); }

        private int _count;
        public int Count { get => _count; set => RaiseAndSetIfChanged(ref _count, value); }

        private TestVm? _child;
        public TestVm? Child { get => _child; set => RaiseAndSetIfChanged(ref _child, value); }

        public bool Fail { get; set; }

        /// <summary>The getter throws while <see cref="Fail"/> is set. The test itself sends the notification.</summary>
        public int Risky => Fail ? throw new InvalidOperationException("Getter error") : Count;
    }

    private sealed class ThrowingEquals
    {
        public override bool Equals(object? obj) => throw new InvalidOperationException("Equals error");

        public override int GetHashCode() => 0;
    }

    private sealed class ThrowingEqualsVm : ReactiveObject
    {
        /// <summary>Without RaiseAndSetIfChanged: it would call Equals itself. The test sends the notification.</summary>
        public ThrowingEquals? Value { get; set; }
    }

    /// <summary>Nine properties: enough for any WhenAnyValue overload.</summary>
    private sealed class NineVm : ReactiveObject
    {
        private int _p1, _p2, _p3, _p4, _p5, _p6, _p7, _p8, _p9;
        public int P1 { get => _p1; set => RaiseAndSetIfChanged(ref _p1, value); }
        public int P2 { get => _p2; set => RaiseAndSetIfChanged(ref _p2, value); }
        public int P3 { get => _p3; set => RaiseAndSetIfChanged(ref _p3, value); }
        public int P4 { get => _p4; set => RaiseAndSetIfChanged(ref _p4, value); }
        public int P5 { get => _p5; set => RaiseAndSetIfChanged(ref _p5, value); }
        public int P6 { get => _p6; set => RaiseAndSetIfChanged(ref _p6, value); }
        public int P7 { get => _p7; set => RaiseAndSetIfChanged(ref _p7, value); }
        public int P8 { get => _p8; set => RaiseAndSetIfChanged(ref _p8, value); }
        public int P9 { get => _p9; set => RaiseAndSetIfChanged(ref _p9, value); }
    }

    #region Subject

    [Fact]
    public void Subject_OnNext_DeliversToSubscribers()
    {
        Subject<int> subject = new();
        List<int> received = new();
        using IDisposable _ = subject.Subscribe(new AnonymousObserver<int>(received.Add));

        subject.OnNext(1);
        subject.OnNext(2);

        Assert.Equal(new[] { 1, 2 }, received);
    }

    [Fact]
    public void Subject_Subscribe_AfterCompleted_GetsOnCompleted()
    {
        Subject<int> subject = new();
        subject.OnCompleted();

        bool completed = false;
        using IDisposable _ = subject.Subscribe(new AnonymousObserver<int>(_ => { }, null, () => completed = true));

        Assert.True(completed);
    }

    [Fact]
    public void Subject_Subscribe_AfterError_GetsOnError()
    {
        Subject<int> subject = new();
        InvalidOperationException error = new("boom");
        subject.OnError(error);

        Exception? received = null;
        using IDisposable _ = subject.Subscribe(new AnonymousObserver<int>(_ => { }, ex => received = ex));

        Assert.Same(error, received);
    }

    [Fact]
    public void Subject_Unsubscribe_StopsDelivery()
    {
        Subject<int> subject = new();
        List<int> received = new();
        IDisposable subscription = subject.Subscribe(new AnonymousObserver<int>(received.Add));

        subject.OnNext(1);
        subscription.Dispose();
        subject.OnNext(2);

        Assert.Equal(new[] { 1 }, received);
    }

    [Fact]
    public void Subject_HasObservers_ReflectsSubscriptions()
    {
        Subject<int> subject = new();
        Assert.False(subject.HasObservers);

        IDisposable subscription = subject.Subscribe(new AnonymousObserver<int>(_ => { }));
        Assert.True(subject.HasObservers);

        subscription.Dispose();
        Assert.False(subject.HasObservers);
    }

    #endregion

    #region BehaviorSubject

    [Fact]
    public void BehaviorSubject_Subscribe_GetsCurrentValue()
    {
        BehaviorSubject<int> subject = new(42);
        int? first = null;
        using IDisposable _ = subject.Subscribe(new AnonymousObserver<int>(v => first ??= v));

        Assert.Equal(42, first);
    }

    [Fact]
    public void BehaviorSubject_OnNext_UpdatesValueAndDelivers()
    {
        BehaviorSubject<int> subject = new(1);
        List<int> received = new();
        using IDisposable _ = subject.Subscribe(new AnonymousObserver<int>(received.Add));

        subject.OnNext(2);

        Assert.Equal(2, subject.Value);
        Assert.Equal(new[] { 1, 2 }, received);
    }

    [Fact]
    public void BehaviorSubject_HasObservers_ReflectsSubscriptions()
    {
        BehaviorSubject<int> subject = new(0);
        Assert.False(subject.HasObservers);

        IDisposable subscription = subject.Subscribe(new AnonymousObserver<int>(_ => { }));
        Assert.True(subject.HasObservers);

        subscription.Dispose();
        Assert.False(subject.HasObservers);
    }

    #endregion

    #region CompositeDisposable

    [Fact]
    public void CompositeDisposable_Dispose_DisposesChildren()
    {
        int disposed = 0;
        CompositeDisposable composite = new(
            Disposable.Create(() => disposed++),
            Disposable.Create(() => disposed++));

        composite.Dispose();

        Assert.Equal(2, disposed);
    }

    [Fact]
    public void CompositeDisposable_Clear_DisposesAndStaysUsable()
    {
        int disposed = 0;
        CompositeDisposable composite = new();
        composite.Add(Disposable.Create(() => disposed++));

        composite.Clear();
        Assert.Equal(1, disposed);
        Assert.Empty(composite);

        // After Clear the container is usable again.
        composite.Add(Disposable.Create(() => disposed++));
        composite.Dispose();
        Assert.Equal(2, disposed);
    }

    [Fact]
    public void CompositeDisposable_AddAfterDispose_DisposesImmediately()
    {
        CompositeDisposable composite = new();
        composite.Dispose();

        bool disposed = false;
        composite.Add(Disposable.Create(() => disposed = true));

        Assert.True(disposed);
    }

    #endregion

    #region WhenAnyValue

    [Fact]
    public void WhenAnyValue_EmitsCurrentThenChanges()
    {
        TestVm vm = new() { Count = 5 };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.Count = 7;

        Assert.Equal(new[] { 5, 7 }, received);
    }

    [Fact]
    public void WhenAnyValue_NestedPath_ReactsToInnerChange()
    {
        TestVm vm = new() { Child = new TestVm { Count = 1 } };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Child!.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.Child!.Count = 2;
        vm.Child = new TestVm { Count = 9 };

        Assert.Equal(new[] { 1, 2, 9 }, received);
    }

    [Fact]
    public void WhenAnyValue_SelectorThrows_SendsOnErrorAndDetaches()
    {
        Subject<string?> subject = new();
        int errorCount = 0;
        using IDisposable _ = subject.Select(s => s!.Length)
            .Subscribe(new AnonymousObserver<int>(_ => { }, _ => errorCount++));

        subject.OnNext(null);
        subject.OnNext(null);

        // After OnError the operator unsubscribes: there is no second error.
        Assert.Equal(1, errorCount);
    }

    [Fact]
    public void WhenAnyValue_TwoProps_SelectorThrows_SendsOnErrorOnceAndDetaches()
    {
        TestVm vm = new() { Name = null, Other = "x" };
        List<int> received = new();
        int errors = 0;

        // Under the hood this is CombineLatest: an exception from the combiner must go to OnError,
        // rather than escape from Subscribe into the calling code. OnError is terminal: nothing arrives afterwards.
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Other, (n, o) => n!.Length + o!.Length)
            .Subscribe(new AnonymousObserver<int>(received.Add, _ => errors++));

        vm.Name = "abc";
        vm.Name = null;
        vm.Other = "yy";

        Assert.Equal(1, errors);
        Assert.Empty(received);
    }

    [Fact]
    public void WhenAnyValue_ReplacedChild_StopsWatchingOldChild()
    {
        TestVm old = new() { Count = 1 };
        TestVm vm = new() { Child = old };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Child!.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.Child = new TestVm { Count = 9 };
        old.Count = 100;

        Assert.Equal(new[] { 1, 9 }, received);
    }

    [Fact]
    public void WhenAnyValue_NullIntermediate_EmitsDefault()
    {
        TestVm vm = new() { Child = new TestVm { Count = 5 } };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Child!.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.Child = null;

        Assert.Equal(new[] { 5, 0 }, received);
    }

    [Fact]
    public void WhenAnyValue_Disposed_StopsDelivery()
    {
        TestVm vm = new() { Count = 1 };
        List<int> received = new();
        IDisposable subscription = vm.WhenAnyValue(x => x.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        subscription.Dispose();
        vm.Count = 2;

        Assert.Equal(new[] { 1 }, received);
    }

    [Fact]
    public void WhenAnyValue_NotificationWithoutChange_SkipsRepeat()
    {
        TestVm vm = new() { Count = 1 };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.RaisePropertyChanged(nameof(TestVm.Count));
        vm.RaisePropertyChanged(null);
        vm.RaisePropertyChanged(string.Empty);
        vm.Count = 2;
        vm.RaisePropertyChanged(nameof(TestVm.Count));

        // As in ReactiveUI: a notification without a value change emits nothing.
        Assert.Equal(new[] { 1, 2 }, received);
    }

    [Fact]
    public void WhenAnyValue_ValueReturnsToEarlier_EmitsEachChange()
    {
        TestVm vm = new() { Count = 1 };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.Count = 2;
        vm.Count = 1;

        // Compared only with the last emitted value, not with all previous ones.
        Assert.Equal(new[] { 1, 2, 1 }, received);
    }

    [Fact]
    public void WhenAnyValue_SubscriberClampsValueInOnNext_ClampHoldsOnRepeat()
    {
        TestVm vm = new() { Count = 5 };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Count).Subscribe(new AnonymousObserver<int>(v =>
        {
            received.Add(v);
            if (v > 10)
                vm.Count = 10;
        }));

        vm.Count = 15;
        vm.Count = 15;

        // The value is stored before emission: a limit inside OnNext also applies on a repeat.
        Assert.Equal(10, vm.Count);
        Assert.Equal(new[] { 5, 15, 10, 15, 10 }, received);
    }

    [Fact]
    public void WhenAnyValue_ConcurrentNotification_InitialValueDeliveredInSubscribe()
    {
        // A notification from another thread during subscription must not intercept the initial value:
        // the subscription emits it on its own thread before Subscribe returns.
        TestVm vm = new() { Name = "a" };
        using CancellationTokenSource stop = new();
        Thread noise = new(() =>
        {
            while (!stop.IsCancellationRequested)
                vm.RaisePropertyChanged(nameof(TestVm.Name));
        }) { IsBackground = true };
        noise.Start();

        int missed = 0;
        try
        {
            int subscriber = Environment.CurrentManagedThreadId;
            for (int i = 0; i < 100_000; i++)
            {
                bool initial = false;
                using IDisposable _ = vm.WhenAnyValue(x => x.Name).Subscribe(new AnonymousObserver<string?>(_ =>
                {
                    if (Environment.CurrentManagedThreadId == subscriber)
                        initial = true;
                }));

                if (!initial)
                    missed++;
            }
        }
        finally
        {
            stop.Cancel();
            noise.Join();
        }

        Assert.Equal(0, missed);
    }

    public static TheoryData<string, bool> OverloadShapes()
    {
        TheoryData<string, bool> data = new();
        foreach (string shape in new[] { "1", "1s", "2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "2t", "3t", "4t", "5t", "6t", "7t", "8u" })
        {
            data.Add(shape, true);
            data.Add(shape, false);
        }

        return data;
    }

    /// <summary>
    /// Each overload passes isDistinct to all of its properties. Shape: number of properties and kind
    /// (s - with selector, t - tuple, u - Unit); no letter means a single property.
    /// </summary>
    [Theory]
    [MemberData(nameof(OverloadShapes))]
    public void WhenAnyValue_EveryOverload_PassesIsDistinct(string shape, bool isDistinct)
    {
        NineVm vm = new();
        int emissions = 0;
        using IDisposable _ = BuildShape(vm, shape, isDistinct).Subscribe(new AnonymousObserver<object?>(_ => emissions++));
        int initial = emissions;

        int properties = int.Parse(shape[..1]);
        for (int i = 1; i <= properties; i++)
            vm.RaisePropertyChanged("P" + i);

        Assert.Equal(isDistinct ? 0 : properties, emissions - initial);
    }

    private static IObservable<object?> Box<T>(IObservable<T> source) => source.Select(v => (object?)v);

    private static IObservable<object?> BuildShape(NineVm vm, string shape, bool d) => shape switch
    {
        "1" => Box(vm.WhenAnyValue(x => x.P1, d)),
        "1s" => Box(vm.WhenAnyValue(x => x.P1, a => a + 0, d)),
        "2s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, (a, b) => a, d)),
        "3s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, (a, b, c) => a, d)),
        "4s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, (a, b, c, e) => a, d)),
        "5s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, (a, b, c, e, f) => a, d)),
        "6s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6,
            (a, b, c, e, f, g) => a, d)),
        "7s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7,
            (a, b, c, e, f, g, h) => a, d)),
        "8s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7, x => x.P8,
            (a, b, c, e, f, g, h, i) => a, d)),
        "9s" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7, x => x.P8, x => x.P9,
            (a, b, c, e, f, g, h, i, j) => a, d)),
        "2t" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, d)),
        "3t" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, d)),
        "4t" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, d)),
        "5t" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, d)),
        "6t" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, d)),
        "7t" => Box(vm.WhenAnyValue(x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7, d)),
        "8u" => Box(vm.WhenAnyValue(d, x => x.P1, x => x.P2, x => x.P3, x => x.P4, x => x.P5, x => x.P6, x => x.P7, x => x.P8)),
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    [Fact]
    public void WhenAnyValue_IsDistinctFalse_EmitsOnEveryNotification()
    {
        TestVm vm = new() { Count = 1 };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Count, isDistinct: false).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.RaisePropertyChanged(nameof(TestVm.Count));
        vm.RaisePropertyChanged(null);
        vm.RaisePropertyChanged(string.Empty);
        vm.RaisePropertyChanged(nameof(TestVm.Name));

        // A repeat without a value change is emitted; another property is not.
        Assert.Equal(new[] { 1, 1, 1, 1 }, received);
    }

    [Fact]
    public void WhenAnyValue_NestedPath_ReplacedWithSameValue_SkipsRepeatAndWatchesNewChild()
    {
        TestVm vm = new() { Child = new TestVm { Count = 1 } };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Child!.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        TestVm replacement = new() { Count = 1 };
        vm.Child = replacement;
        replacement.Count = 3;

        // Replacing a link with the same value stays silent, but the subscription is now on the new object.
        Assert.Equal(new[] { 1, 3 }, received);
    }

    [Fact]
    public void WhenAnyValue_NullIntermediate_SameAsDefault_SkipsRepeat()
    {
        TestVm vm = new() { Child = new TestVm { Count = 0 } };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Child!.Count).Subscribe(new AnonymousObserver<int>(received.Add));

        vm.Child = null;
        vm.Child = new TestVm { Count = 4 };

        Assert.Equal(new[] { 0, 4 }, received);
    }

    [Fact]
    public void WhenAnyValue_Selector_NotCalledOnNotificationWithoutChange()
    {
        TestVm vm = new() { Count = 1 };
        int calls = 0;
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Count, count => { calls++; return count * 10; })
            .Subscribe(new AnonymousObserver<int>(received.Add));

        vm.RaisePropertyChanged(nameof(TestVm.Count));
        vm.Count = 2;

        Assert.Equal(2, calls);
        Assert.Equal(new[] { 10, 20 }, received);
    }

    [Fact]
    public void WhenAnyValue_ConversionInExpression_SkipsRepeat()
    {
        TestVm vm = new() { Count = 2 };
        List<double> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => (double)x.Count).Subscribe(new AnonymousObserver<double>(received.Add));

        vm.RaisePropertyChanged(nameof(TestVm.Count));
        vm.Count = 3;

        Assert.Equal(new double[] { 2, 3 }, received);
    }

    [Fact]
    public void WhenAnyValue_EqualsThrows_SendsOnErrorInsteadOfThrowingToCaller()
    {
        ThrowingEqualsVm vm = new() { Value = new ThrowingEquals() };
        List<ThrowingEquals?> received = new();
        int errors = 0;
        using IDisposable _ = vm.WhenAnyValue(x => x.Value)
            .Subscribe(new AnonymousObserver<ThrowingEquals?>(received.Add, _ => errors++));

        // Comparing with the previous value calls the model's Equals: its exception goes to the subscriber,
        // not to the code that sent the notification.
        vm.RaisePropertyChanged(nameof(ThrowingEqualsVm.Value));
        vm.RaisePropertyChanged(nameof(ThrowingEqualsVm.Value));

        Assert.Single(received);
        Assert.Equal(1, errors);
    }

    [Fact]
    public void WhenAnyValue_GetterThrows_SendsOnErrorOnceAndDetaches()
    {
        TestVm vm = new() { Count = 1 };
        List<int> received = new();
        int errors = 0;
        using IDisposable _ = vm.WhenAnyValue(x => x.Risky).Subscribe(new AnonymousObserver<int>(received.Add, _ => errors++));

        vm.Fail = true;
        vm.RaisePropertyChanged(nameof(TestVm.Risky));
        vm.Fail = false;
        vm.RaisePropertyChanged(nameof(TestVm.Risky));

        Assert.Equal(new[] { 1 }, received);
        Assert.Equal(1, errors);
    }

    [Fact]
    public void WhenAnyValue_ConversionInExpression_ConvertsValue()
    {
        TestVm vm = new() { Count = 2 };
        List<long> longs = new();
        List<double> doubles = new();
        Exception? error = null;
        using IDisposable a = vm.WhenAnyValue<TestVm, long>(x => x.Count)
            .Subscribe(new AnonymousObserver<long>(longs.Add, ex => error = ex));
        using IDisposable b = vm.WhenAnyValue(x => (double)x.Count)
            .Subscribe(new AnonymousObserver<double>(doubles.Add, ex => error = ex));

        vm.Count = 3;

        Assert.Null(error);
        Assert.Equal(new long[] { 2, 3 }, longs);
        Assert.Equal(new double[] { 2, 3 }, doubles);
    }

    [Fact]
    public void WhenAnyValue_TwoProps_NoSelector_EmitsTupleOnceThenChanges()
    {
        TestVm vm = new() { Name = "a", Count = 1 };
        List<(string?, int)> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Count)
            .Subscribe(new AnonymousObserver<(string?, int)>(received.Add));

        vm.Count = 2;
        vm.Name = "b";

        // As in ReactiveUI: one value on subscription, not a signal per property.
        Assert.Equal(new (string?, int)[] { ("a", 1), ("a", 2), ("b", 2) }, received);
    }

    [Fact]
    public void WhenAnyValue_TwoProps_NoSelector_SkipsRepeats()
    {
        TestVm vm = new() { Count = 1 };
        List<(string?, int)> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Count)
            .Subscribe(new AnonymousObserver<(string?, int)>(received.Add));

        vm.Count = 2;
        vm.RaisePropertyChanged(nameof(TestVm.Count));
        vm.RaisePropertyChanged(null);
        vm.Count = 3;

        // Repeats are filtered per property, so DistinctUntilChanged after the tuple is unnecessary.
        Assert.Equal(new (string?, int)[] { (null, 1), (null, 2), (null, 3) }, received);
    }

    [Fact]
    public void WhenAnyValue_TwoProps_NoSelector_IsDistinctFalse_EmitsOnEveryNotification()
    {
        TestVm vm = new() { Count = 1 };
        List<(string?, int)> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Count, isDistinct: false)
            .Subscribe(new AnonymousObserver<(string?, int)>(received.Add));

        vm.RaisePropertyChanged(nameof(TestVm.Count));

        Assert.Equal(new (string?, int)[] { (null, 1), (null, 1) }, received);
    }

    [Fact]
    public void WhenAnyValue_TwoProps_Selector_ResultNotCompared()
    {
        TestVm vm = new() { Name = "a", Count = 1 };
        List<bool> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Count, (name, count) => name is not null && count > 0)
            .Subscribe(new AnonymousObserver<bool>(received.Add));

        vm.Count = 2;
        vm.RaisePropertyChanged(nameof(TestVm.Name));

        // As in ReactiveUI: the property changed, and the same selector result is emitted again.
        // A notification without a value change does not invoke the selector.
        Assert.Equal(new[] { true, true }, received);
    }

    [Fact]
    public void WhenAnyValue_TwoProps_Selector_IsDistinctFalse_EmitsOnEveryNotification()
    {
        TestVm vm = new() { Name = "a", Count = 1 };
        List<int> received = new();
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Count, (name, count) => name!.Length + count, isDistinct: false)
            .Subscribe(new AnonymousObserver<int>(received.Add));

        vm.RaisePropertyChanged(nameof(TestVm.Name));

        Assert.Equal(new[] { 2, 2 }, received);
    }

    [Fact]
    public void WhenAnyValue_SevenProps_NoSelector_EmitsTuple()
    {
        TestVm vm = new() { Name = "n", Other = "o", Count = 1 };
        (string?, string?, int, TestVm?, string?, string?, int) last = default;
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Other, x => x.Count, x => x.Child,
                x => x.Name, x => x.Other, x => x.Count)
            .Subscribe(new AnonymousObserver<(string?, string?, int, TestVm?, string?, string?, int)>(v => last = v));

        vm.Count = 5;

        Assert.Equal(("n", "o", 5, (TestVm?)null, "n", "o", 5), last);
    }

    [Fact]
    public void WhenAnyValue_EightProps_NoSelector_SignalsPerProperty()
    {
        // There are no tuple overloads for eight properties (as in ReactiveUI): such a call yields Unit.
        TestVm vm = new();
        int signals = 0;
        IObservable<Unit> changed = vm.WhenAnyValue(x => x.Name, x => x.Other, x => x.Count, x => x.Child,
            x => x.Name, x => x.Other, x => x.Count, x => x.Child);
        using IDisposable _ = changed.Subscribe(new AnonymousObserver<Unit>(_ => signals++));

        vm.Count = 1;

        Assert.Equal(8 + 2, signals);
    }

    [Fact]
    public void WhenAnyValue_EightProps_NotificationWithoutChange_NoSignal()
    {
        TestVm vm = new();
        int signals = 0;
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Other, x => x.Count, x => x.Child,
                x => x.Name, x => x.Other, x => x.Count, x => x.Child)
            .Subscribe(new AnonymousObserver<Unit>(_ => signals++));

        vm.RaisePropertyChanged(nameof(TestVm.Count));
        vm.RaisePropertyChanged(null);

        Assert.Equal(8, signals);
    }

    [Fact]
    public void WhenAnyValue_EightProps_IsDistinctFalse_SignalsOnEveryNotification()
    {
        TestVm vm = new();
        int signals = 0;
        using IDisposable _ = vm.WhenAnyValue(false, x => x.Name, x => x.Other, x => x.Count, x => x.Child,
                x => x.Name, x => x.Other, x => x.Count, x => x.Child)
            .Subscribe(new AnonymousObserver<Unit>(_ => signals++));

        vm.RaisePropertyChanged(nameof(TestVm.Count));

        Assert.Equal(8 + 2, signals);
    }

    [Fact]
    public void WhenAnyValue_EightProps_GetterThrows_SendsOnError()
    {
        TestVm vm = new();
        int errors = 0;
        using IDisposable _ = vm.WhenAnyValue(x => x.Name, x => x.Other, x => x.Count, x => x.Child,
                x => x.Name, x => x.Other, x => x.Count, x => x.Risky)
            .Subscribe(new AnonymousObserver<Unit>(_ => { }, _ => errors++));

        vm.Fail = true;
        vm.RaisePropertyChanged(nameof(TestVm.Risky));

        Assert.Equal(1, errors);
    }

    #endregion

    #region Operators: delegate exception handling

    [Fact]
    public void Where_PredicateThrows_SendsOnErrorOnceAndDetaches()
    {
        Subject<int> subject = new();
        int errorCount = 0;
        using IDisposable _ = subject.Where(x => x == 0 ? throw new InvalidOperationException() : true)
            .Subscribe(new AnonymousObserver<int>(_ => { }, _ => errorCount++));

        subject.OnNext(0);
        subject.OnNext(0);

        Assert.Equal(1, errorCount);
    }

    #endregion
}
