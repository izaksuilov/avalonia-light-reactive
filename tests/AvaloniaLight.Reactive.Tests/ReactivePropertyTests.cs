namespace AvaloniaLight.Reactive.Tests;

/// <summary>
/// Properties with the [Reactive] attribute in live code: the generator is attached to these tests as an analyzer,
/// so this checks the same output a package consumer gets.
/// </summary>
public partial class ReactivePropertyTests
{
    private sealed partial class TestVm : ReactiveObject
    {
        [Reactive] public partial string? Name { get; set; }

        [Reactive] public partial int Count { get; private set; }

        public void Increment() => Count++;
    }

    private static List<string> Record(TestVm vm, out List<string> changing)
    {
        List<string> changed = new();
        List<string> before = new();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        vm.PropertyChanging += (_, e) => before.Add(e.PropertyName ?? string.Empty);
        changing = before;
        return changed;
    }

    [Fact]
    public void ReactiveProperty_NewValue_RaisesChangingAndChanged()
    {
        TestVm vm = new();
        List<string> changed = Record(vm, out List<string> changing);

        vm.Name = "First";

        Assert.Equal("First", vm.Name);
        Assert.Equal(new[] { nameof(TestVm.Name) }, changing);
        Assert.Equal(new[] { nameof(TestVm.Name) }, changed);
    }

    [Fact]
    public void ReactiveProperty_SameValue_NoNotification()
    {
        TestVm vm = new() { Name = "Name" };
        List<string> changed = Record(vm, out List<string> changing);

        vm.Name = "Name";

        Assert.Empty(changing);
        Assert.Empty(changed);
    }

    [Fact]
    public void ReactiveProperty_PrivateSetter_RaisesChanged()
    {
        TestVm vm = new();
        List<string> changed = Record(vm, out _);

        vm.Increment();
        vm.Increment();

        Assert.Equal(2, vm.Count);
        Assert.Equal(new[] { nameof(TestVm.Count), nameof(TestVm.Count) }, changed);
    }
}
