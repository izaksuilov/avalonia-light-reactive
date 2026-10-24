using System.ComponentModel;
using LightReactive.Avalonia.Validation;

namespace LightReactive.Avalonia.Tests;

/// <summary>
/// Tests for the lazy validation context: a model without rules never creates a context,
/// the first access from several threads yields a single context, and no new context is created after Dispose.
/// </summary>
public class ValidationContextLazyTests
{
    private const string NameError = "Enter a name";
    private const int RaceIterations = 200;
    private const int RaceThreads = 4;

    private sealed class TestVm : ReactiveValidationObject
    {
        private string? _name;
        public string? Name { get => _name; set => RaiseAndSetIfChanged(ref _name, value); }
    }

    /// <summary>
    /// A model with its own IValidatableViewModel implementation, without ReactiveValidationObject: it owns its context, which exists immediately.
    /// </summary>
    private sealed class OwnVm : ReactiveObject, IValidatableViewModel
    {
        private string? _name;
        public string? Name { get => _name; set => RaiseAndSetIfChanged(ref _name, value); }

        public ValidationContext ValidationContext { get; } = new();
    }

    private static ValidationHelper NameRule(TestVm vm)
        => vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), NameError);

    private static void AssertDisposed(ValidationContext context)
        => Assert.Throws<ObjectDisposedException>(() => context.Valid.Subscribe(new AnonymousObserver<bool>(_ => { })));

    /// <summary>
    /// Runs the actions on separate threads simultaneously (via a barrier) and waits for them to complete.
    /// </summary>
    private static void RunTogether(params Action[] actions)
    {
        using Barrier barrier = new(actions.Length);
        Exception? error = null;
        Thread[] threads = actions
            .Select(action => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    action();
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref error, ex, null);
                }
            }))
            .ToArray();

        foreach (Thread thread in threads)
            thread.Start();
        foreach (Thread thread in threads)
            thread.Join();

        if (error is not null)
            throw new AggregateException(error);
    }

    #region Model without rules

    [Fact]
    public void NewModel_WithoutRules_DoesNotCreateContext()
    {
        TestVm vm = new();
        int errorsChanged = 0;
        vm.ErrorsChanged += (_, _) => errorsChanged++;

        vm.Name = "a";
        bool hasErrors = vm.HasErrors;
        object[] allErrors = vm.GetErrors(null).Cast<object>().ToArray();
        object[] nameErrors = vm.GetErrors(nameof(TestVm.Name)).Cast<object>().ToArray();
        vm.ClearValidationRules();
        vm.ClearValidationRules(x => x.Name);

        Assert.Null(vm.ExistingValidationContext);
        Assert.False(hasErrors);
        Assert.Empty(allErrors);
        Assert.Empty(nameErrors);
        Assert.Equal(0, errorsChanged);
    }

    [Fact]
    public void ValidationRule_FirstRule_CreatesContextOnce()
    {
        TestVm vm = new();

        NameRule(vm);
        ValidationContext? first = vm.ExistingValidationContext;
        vm.ValidationRule(x => x.Name, name => name != "x", "Not x");

        Assert.NotNull(first);
        Assert.Same(first, vm.ValidationContext);
        Assert.Same(first, vm.ExistingValidationContext);
        Assert.NotSame(ValidationContext.DisposedInstance, first);
    }

    [Fact]
    public void ValidationContext_FirstRead_CreatesWorkingContext()
    {
        TestVm vm = new();
        ValidationContext context = vm.ValidationContext;
        List<bool> valid = new();
        using IDisposable _ = context.Valid.Subscribe(new AnonymousObserver<bool>(valid.Add));

        NameRule(vm);

        Assert.Same(context, vm.ExistingValidationContext);
        Assert.Equal(new[] { true, false }, valid);
        Assert.True(vm.HasErrors);
    }

    #endregion

    #region Disposal

    [Fact]
    public void Dispose_WithoutContext_ReturnsSharedDisposedContext()
    {
        TestVm first = new();
        TestVm second = new();

        first.Dispose();
        second.Dispose();

        Assert.Same(ValidationContext.DisposedInstance, first.ValidationContext);
        Assert.Same(ValidationContext.DisposedInstance, second.ValidationContext);
        Assert.True(first.ValidationContext.IsValid);
        Assert.False(first.HasErrors);
        Assert.Empty(first.GetErrors(null).Cast<object>());
        AssertDisposed(first.ValidationContext);
    }

    [Fact]
    public void Dispose_WithoutContext_RuleAfterDisposeIsIgnoredAndUnsubscribed()
    {
        TestVm vm = new();
        vm.Dispose();
        Subject<string?> source = new();
        int errorsChanged = 0;
        vm.ErrorsChanged += (_, _) => errorsChanged++;

        ValidationHelper rule = vm.ValidationRule(source, value => !string.IsNullOrEmpty(value), _ => NameError);
        source.OnNext(null);

        Assert.False(source.HasObservers);
        Assert.False(vm.HasErrors);
        Assert.Empty(vm.GetErrors(null).Cast<object>());
        Assert.Equal(0, errorsChanged);
        rule.Dispose();
    }

    [Fact]
    public void Dispose_Twice_KeepsSharedDisposedContext()
    {
        TestVm vm = new();

        vm.Dispose();
        vm.Dispose();

        Assert.Same(ValidationContext.DisposedInstance, vm.ValidationContext);
    }

    [Fact]
    public void Dispose_WithContext_KeepsOwnDisposedContext()
    {
        TestVm vm = new();
        Subject<string?> source = new();
        NameRule(vm);
        vm.ValidationRule(x => x.Name, source, value => !string.IsNullOrEmpty(value), _ => NameError);
        ValidationContext context = vm.ValidationContext;
        int notifications = 0;
        vm.ErrorsChanged += (_, _) => notifications++;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TestVm.HasErrors))
                notifications++;
        };

        vm.Dispose();
        vm.Name = "a";
        source.OnNext("a");

        // As before the lazy context: the model keeps its own disposed context,
        // rules are unsubscribed, HasErrors keeps the last result, and no more events are raised.
        Assert.Same(context, vm.ValidationContext);
        Assert.NotSame(ValidationContext.DisposedInstance, context);
        Assert.False(source.HasObservers);
        Assert.True(vm.HasErrors);
        Assert.Empty(vm.GetErrors(null).Cast<object>());
        Assert.Equal(0, notifications);
        AssertDisposed(context);
    }

    [Fact]
    public void DisposedContext_IgnoresNewPropertyChangedSubscribers()
    {
        ValidationContext live = new();
        ValidationContext disposed = new();
        disposed.Dispose();
        int calls = 0;
        PropertyChangedEventHandler handler = (_, _) => calls++;

        // A live context records the subscriber, which shows that subscribing through the interface works.
        ((INotifyPropertyChanged)live).PropertyChanged += handler;
        live.RaisePropertyChanged(nameof(ValidationContext.IsValid));
        int liveCalls = calls;

        // A disposed context does not change, whereas a shared disposed one would accumulate subscribers forever.
        ((INotifyPropertyChanged)disposed).PropertyChanged += handler;
        ((INotifyPropertyChanged)ValidationContext.DisposedInstance).PropertyChanged += handler;
        disposed.RaisePropertyChanged(nameof(ValidationContext.IsValid));
        ValidationContext.DisposedInstance.RaisePropertyChanged(nameof(ValidationContext.IsValid));
        ((INotifyPropertyChanged)ValidationContext.DisposedInstance).PropertyChanged -= handler;

        Assert.Equal(1, liveCalls);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void LiveContext_WhenAnyValueThroughContext_SeesIsValidChanges()
    {
        TestVm vm = new() { Name = "a" };
        NameRule(vm);
        List<bool> isValid = new();

        using IDisposable _ = vm.WhenAnyValue(x => x.ValidationContext.IsValid).Subscribe(new AnonymousObserver<bool>(isValid.Add));
        vm.Name = null;
        vm.Name = "b";

        Assert.Equal(new[] { true, false, true }, isValid);
    }

    #endregion

    #region Custom IValidatableViewModel implementation

    [Fact]
    public void ClearValidationRules_OwnViewModel_RemovesRulesFromItsContext()
    {
        const string CodeError = "Enter a code";
        OwnVm vm = new();
        Subject<string?> code = new();
        vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), NameError);
        vm.ValidationRule(code, value => !string.IsNullOrEmpty(value), _ => CodeError);
        code.OnNext(null);
        ValidationContext context = vm.ValidationContext;
        int isValidChanges = 0;
        ((INotifyPropertyChanged)context).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ValidationContext.IsValid))
                isValidChanges++;
        };
        List<bool> valid = new();
        using IDisposable _ = context.Valid.Subscribe(new AnonymousObserver<bool>(valid.Add));

        // Only the rules of the Name property are removed: the code rule keeps the context invalid.
        vm.ClearValidationRules(x => x.Name);
        string[] afterNameClear = context.GetMessages(null).ToArray();
        bool validAfterNameClear = context.IsValid;

        // Removed rules no longer change anything.
        vm.ClearValidationRules();
        vm.Name = "a";
        vm.Name = null;
        code.OnNext(null);

        Assert.Equal(new[] { CodeError }, afterNameClear);
        Assert.False(validAfterNameClear);
        Assert.True(context.IsValid);
        Assert.Empty(context.GetMessages(null));
        Assert.False(code.HasObservers);
        Assert.Equal(1, isValidChanges);
        Assert.Equal(new[] { false, true }, valid);
    }

    #endregion

    #region First-access race

    [Fact]
    public void FirstAccess_FromSeveralThreads_GivesOneContext()
    {
        for (int i = 0; i < RaceIterations; i++)
        {
            TestVm vm = new();
            ValidationContext?[] seen = new ValidationContext?[RaceThreads];

            RunTogether(Enumerable.Range(0, RaceThreads)
                .Select(index => (Action)(() => seen[index] = vm.ValidationContext))
                .ToArray());

            ValidationContext? context = vm.ExistingValidationContext;
            Assert.NotNull(context);
            Assert.All(seen, item => Assert.Same(context, item));
        }
    }

    [Fact]
    public void FirstAccess_RaceWithRule_ModelGetsErrorEvents()
    {
        for (int i = 0; i < RaceIterations; i++)
        {
            TestVm vm = new();
            int errorsChanged = 0;
            int hasErrorsChanged = 0;
            vm.ErrorsChanged += (_, _) => Interlocked.Increment(ref errorsChanged);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TestVm.HasErrors))
                    Interlocked.Increment(ref hasErrorsChanged);
            };

            RunTogether(
                () => _ = vm.ValidationContext,
                () => NameRule(vm),
                () => _ = vm.HasErrors);

            // The context is subscribed to the model before publication: whichever thread created it, the rule reports the error.
            Assert.True(vm.HasErrors);
            Assert.Equal(new[] { NameError }, vm.GetErrors(nameof(TestVm.Name)).Cast<string>());
            Assert.Equal(1, Volatile.Read(ref errorsChanged));
            Assert.Equal(1, Volatile.Read(ref hasErrorsChanged));
        }
    }

    [Fact]
    public void FirstAccess_RaceWithDispose_LeavesOnlyDisposedContext()
    {
        for (int i = 0; i < RaceIterations; i++)
        {
            TestVm vm = new();

            RunTogether(
                vm.Dispose,
                () => _ = vm.ValidationContext);

            // Whoever wins the race, the model has no live context after Dispose.
            ValidationContext context = vm.ValidationContext;
            AssertDisposed(context);
            NameRule(vm);
            Assert.False(vm.HasErrors);
        }
    }

    #endregion
}
