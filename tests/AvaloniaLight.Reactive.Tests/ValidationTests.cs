using System.ComponentModel;
using AvaloniaLight.Reactive.Validation;

namespace AvaloniaLight.Reactive.Tests;

/// <summary>
/// Tests for removing validation rules (ClearValidationRules) and re-evaluating a property rule.
/// </summary>
public class ValidationTests
{
    private sealed class TestVm : ReactiveValidationObject
    {
        private string? _name;
        public string? Name { get => _name; set => RaiseAndSetIfChanged(ref _name, value); }

        private int _count;
        public int Count { get => _count; set => RaiseAndSetIfChanged(ref _count, value); }
    }

    [Fact]
    public void ClearValidationRules_RemovesAllRulesAndErrors()
    {
        TestVm vm = new();
        vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Enter a name");
        vm.ValidationRule(x => x.Count, count => count > 0, "Must be greater than zero");
        Assert.False(vm.ValidationContext.IsValid);

        List<string?> errorsChanged = new();
        vm.ErrorsChanged += (_, e) => errorsChanged.Add(e.PropertyName);

        vm.ClearValidationRules();

        Assert.True(vm.ValidationContext.IsValid);
        Assert.False(vm.HasErrors);
        Assert.Empty(vm.GetErrors(null));
        Assert.Equal(new[] { nameof(TestVm.Name), nameof(TestVm.Count) }, errorsChanged);
    }

    [Fact]
    public void ClearValidationRules_RemovedRuleStopsWatchingProperty()
    {
        TestVm vm = new() { Name = "a" };
        Subject<string?> source = new();
        vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Enter a name");
        vm.ValidationRule(source, value => !string.IsNullOrEmpty(value), _ => "Empty value");
        Assert.True(source.HasObservers);

        vm.ClearValidationRules();
        int notifications = 0;
        vm.ErrorsChanged += (_, _) => notifications++;
        vm.Name = null;

        // A removed rule is unsubscribed from its source: a property change does not reach the rule
        // and produces no error notifications.
        Assert.False(source.HasObservers);
        Assert.Equal(0, notifications);
        Assert.True(vm.ValidationContext.IsValid);
    }

    [Fact]
    public void ClearValidationRules_ContextAcceptsNewRulesAndKeepsValidSubscribers()
    {
        TestVm vm = new();
        vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Enter a name");
        List<bool> valid = new();
        using IDisposable _ = vm.ValidationContext.Valid.Subscribe(new AnonymousObserver<bool>(valid.Add));

        vm.ClearValidationRules();
        vm.ValidationRule(x => x.Count, count => count > 0, "Must be greater than zero");

        Assert.Equal(new[] { false, true, false }, valid);
        Assert.Equal(new[] { "Must be greater than zero" }, vm.GetErrors(nameof(TestVm.Count)).Cast<string>());
    }

    [Fact]
    public void ClearValidationRules_ByProperty_KeepsOtherRules()
    {
        TestVm vm = new();
        vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Enter a name");
        vm.ValidationRule(x => x.Count, count => count > 0, "Must be greater than zero");

        vm.ClearValidationRules(x => x.Name);

        Assert.Empty(vm.GetErrors(nameof(TestVm.Name)));
        Assert.Equal(new[] { "Must be greater than zero" }, vm.GetErrors(nameof(TestVm.Count)).Cast<string>());
        Assert.False(vm.ValidationContext.IsValid);
    }

    [Fact]
    public void ValidationRule_NotificationWithoutChange_DoesNotRevalidate()
    {
        TestVm vm = new() { Name = "a" };
        int checks = 0;
        vm.ValidationRule(x => x.Name, name => { checks++; return !string.IsNullOrEmpty(name); }, "Enter a name");

        vm.RaisePropertyChanged(nameof(TestVm.Name));
        vm.Name = null;

        // As in ReactiveUI.Validation: a property rule is re-evaluated only when the value changes.
        Assert.Equal(2, checks);
        Assert.False(vm.ValidationContext.IsValid);
    }

    [Fact]
    public void ValidationHelper_DisposeAfterClear_DoesNotNotify()
    {
        TestVm vm = new();
        ValidationHelper rule = vm.ValidationRule(x => x.Name, name => !string.IsNullOrEmpty(name), "Enter a name");
        vm.ClearValidationRules();

        int notifications = 0;
        vm.ErrorsChanged += (_, _) => notifications++;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, _) => notifications++;

        rule.Dispose();

        Assert.Equal(0, notifications);
    }
}
