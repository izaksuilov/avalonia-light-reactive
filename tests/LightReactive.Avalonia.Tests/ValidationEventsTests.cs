using System.ComponentModel;
using LightReactive.Avalonia.Validation;

namespace LightReactive.Avalonia.Tests;

/// <summary>
/// Tests for validation events: ErrorsChanged, HasErrors and IsValid are raised only when the errors actually change.
/// </summary>
public class ValidationEventsTests
{
    private const string NameError = "Enter a name";
    private const string CountError = "Must be greater than zero";

    private sealed class TestVm : ReactiveValidationObject
    {
        private string? _name;
        public string? Name { get => _name; set => RaiseAndSetIfChanged(ref _name, value); }

        private int _count;
        public int Count { get => _count; set => RaiseAndSetIfChanged(ref _count, value); }

        private object _marker = new();
        public object Marker { get => _marker; set => RaiseAndSetIfChanged(ref _marker, value); }
    }

    /// <summary>
    /// Records the model's error events: property names from ErrorsChanged and the number of HasErrors notifications.
    /// </summary>
    private sealed class ErrorEvents
    {
        public ErrorEvents(TestVm vm)
        {
            vm.ErrorsChanged += (_, e) => Properties.Add(e.PropertyName);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TestVm.HasErrors))
                    HasErrorsChanges++;
            };
        }

        public List<string?> Properties { get; } = new();

        public int HasErrorsChanges { get; private set; }
    }

    private static ValidationHelper NameRule(TestVm vm)
        => vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), NameError);

    private static ValidationHelper CountRule(TestVm vm)
        => vm.ValidationRule(x => x.Count, count => count > 0, CountError);

    #region Adding a rule

    [Fact]
    public void ValidationRule_ValidRule_RaisesNothing()
    {
        TestVm vm = new() { Name = "a" };
        ErrorEvents events = new(vm);

        NameRule(vm);

        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void ValidationRule_InvalidRule_RaisesErrorsChangedAndHasErrors()
    {
        TestVm vm = new();
        ErrorEvents events = new(vm);

        NameRule(vm);

        Assert.Equal(new[] { nameof(TestVm.Name) }, events.Properties);
        Assert.Equal(1, events.HasErrorsChanges);
        Assert.True(vm.HasErrors);
        Assert.Equal(new[] { NameError }, vm.GetErrors(nameof(TestVm.Name)).Cast<string>());
    }

    [Fact]
    public void ValidationRule_InvalidRuleWhenAlreadyInvalid_RaisesErrorsChangedOnly()
    {
        TestVm vm = new();
        NameRule(vm);
        ErrorEvents events = new(vm);

        CountRule(vm);

        // The error list of the Count property changed, but the overall result stayed invalid.
        Assert.Equal(new[] { nameof(TestVm.Count) }, events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void ValidationRule_ValidRuleToInvalidContext_RaisesNothing()
    {
        TestVm vm = new() { Count = 1 };
        NameRule(vm);
        ErrorEvents events = new(vm);

        CountRule(vm);

        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
        Assert.True(vm.HasErrors);
    }

    #endregion

    #region Rule result change

    [Fact]
    public void RuleResult_ValidityFlips_RaisesErrorsChangedAndHasErrors()
    {
        TestVm vm = new() { Name = "a" };
        NameRule(vm);
        ErrorEvents events = new(vm);

        vm.Name = null;
        Assert.Equal(new[] { nameof(TestVm.Name) }, events.Properties);
        Assert.Equal(1, events.HasErrorsChanges);
        Assert.True(vm.HasErrors);

        vm.Name = "b";
        Assert.Equal(new[] { nameof(TestVm.Name), nameof(TestVm.Name) }, events.Properties);
        Assert.Equal(2, events.HasErrorsChanges);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void RuleResult_MessageChangedOnly_RaisesErrorsChangedWithoutHasErrors()
    {
        TestVm vm = new() { Name = "x" };
        vm.ValidationRule(x => x.Name, name => name == "ok", name => $"Bad name {name}");
        ErrorEvents events = new(vm);

        vm.Name = "y";

        Assert.Equal(new[] { nameof(TestVm.Name) }, events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
        Assert.Equal(new[] { "Bad name y" }, vm.GetErrors(nameof(TestVm.Name)).Cast<string>());
    }

    [Fact]
    public void RuleResult_SameResult_RaisesNothing()
    {
        TestVm vm = new() { Name = "a" };
        NameRule(vm);
        ErrorEvents events = new(vm);

        vm.Name = "b";

        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
    }

    [Fact]
    public void RuleResult_OtherRuleKeepsModelInvalid_HasErrorsNotRaised()
    {
        TestVm vm = new();
        NameRule(vm);
        CountRule(vm);
        ErrorEvents events = new(vm);

        vm.Count = 1;

        Assert.Equal(new[] { nameof(TestVm.Count) }, events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
        Assert.True(vm.HasErrors);
    }

    /// <summary>
    /// Forced revalidation, like a Revalidate method on a model: the rule listens to a property and a marker,
    /// and is re-evaluated when the marker changes. Events are raised only if the rule result changed.
    /// </summary>
    [Fact]
    public void Revalidate_MarkerChanged_RaisesOnlyWhenResultChanges()
    {
        bool allowed = true;
        TestVm vm = new() { Name = "a" };
        vm.ValidationRule(x => x.Name,
                          vm.WhenAnyValue(x => x.Name, x => x.Marker, (name, _) => name),
                          _ => allowed,
                          _ => NameError);
        ErrorEvents events = new(vm);

        vm.Marker = new();
        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);

        allowed = false;
        vm.Marker = new();
        Assert.Equal(new[] { nameof(TestVm.Name) }, events.Properties);
        Assert.Equal(1, events.HasErrorsChanges);
        Assert.True(vm.HasErrors);
    }

    #endregion

    #region Removing rules

    [Fact]
    public void ValidationHelperDispose_ValidRule_RaisesNothing()
    {
        TestVm vm = new() { Name = "a" };
        ValidationHelper rule = NameRule(vm);
        ErrorEvents events = new(vm);

        rule.Dispose();

        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
    }

    [Fact]
    public void ValidationHelperDispose_InvalidRule_RaisesErrorsChangedAndHasErrors()
    {
        TestVm vm = new();
        ValidationHelper rule = NameRule(vm);
        ErrorEvents events = new(vm);

        rule.Dispose();

        Assert.Equal(new[] { nameof(TestVm.Name) }, events.Properties);
        Assert.Equal(1, events.HasErrorsChanges);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void ValidationHelperDispose_RemovedRule_DoesNotNotifyAnymore()
    {
        TestVm vm = new() { Name = "a" };
        ValidationHelper rule = NameRule(vm);
        rule.Dispose();
        ErrorEvents events = new(vm);

        vm.Name = null;

        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
        Assert.False(vm.HasErrors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnComponentChanged_RuleRemovedAfterReadingContext_RaisesNothing(bool clearAll)
    {
        TestVm vm = new();
        ValidationContext context = vm.ValidationContext;
        Subject<string?> source = new();
        ValidationComponent<string?> component = new(nameof(TestVm.Name), source, name => !string.IsNullOrEmpty(name), _ => NameError);
        context.Add(component);
        source.OnNext(null);

        // The rule changed its result and read its context while another thread removed it.
        ValidationContext? seen = component.Context;
        if (clearAll)
            vm.ClearValidationRules();
        else
            context.Remove(component);
        ErrorEvents events = new(vm);

        seen?.OnComponentChanged(component);

        Assert.Same(context, seen);
        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public void ClearValidationRules_OnlyValidRules_RaisesNothing()
    {
        TestVm vm = new() { Name = "a", Count = 1 };
        NameRule(vm);
        CountRule(vm);
        ErrorEvents events = new(vm);

        vm.ClearValidationRules();

        Assert.Empty(events.Properties);
        Assert.Equal(0, events.HasErrorsChanges);
    }

    [Fact]
    public void ClearValidationRules_ValidAndInvalidRules_RaisesOnlyForInvalidProperties()
    {
        TestVm vm = new() { Count = 1 };
        NameRule(vm);
        CountRule(vm);
        ErrorEvents events = new(vm);

        vm.ClearValidationRules();

        Assert.Equal(new[] { nameof(TestVm.Name) }, events.Properties);
        Assert.Equal(1, events.HasErrorsChanges);
        Assert.False(vm.HasErrors);
    }

    #endregion

    #region Context result

    [Fact]
    public void ValidationContext_IsValidAndValid_NotifyOnlyOnChange()
    {
        TestVm vm = new() { Name = "a" };
        ValidationContext context = vm.ValidationContext;
        int isValidChanges = 0;
        ((INotifyPropertyChanged)context).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ValidationContext.IsValid))
                isValidChanges++;
        };
        List<bool> valid = new();
        using IDisposable _ = context.Valid.Subscribe(new AnonymousObserver<bool>(valid.Add));

        NameRule(vm);
        CountRule(vm);
        vm.Count = 2;
        vm.Count = 3;

        Assert.Equal(2, isValidChanges);
        Assert.Equal(new[] { true, false, true }, valid);
        Assert.True(context.IsValid);
    }

    [Fact]
    public void ValidationContext_ValidSubscribedAfterError_StartsWithCurrentResult()
    {
        TestVm vm = new();
        NameRule(vm);
        List<bool> valid = new();

        using IDisposable _ = vm.ValidationContext.Valid.Subscribe(new AnonymousObserver<bool>(valid.Add));
        vm.Name = "a";

        Assert.Equal(new[] { false, true }, valid);
    }

    #endregion
}
