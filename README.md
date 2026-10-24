# LightReactive.Avalonia

[![NuGet](https://img.shields.io/nuget/v/LightReactive.Avalonia.svg)](https://www.nuget.org/packages/LightReactive.Avalonia)
[![NuGet downloads](https://img.shields.io/nuget/dt/LightReactive.Avalonia.svg)](https://www.nuget.org/packages/LightReactive.Avalonia)
[![CI](https://github.com/izaksuilov/LightReactive.Avalonia/actions/workflows/ci.yml/badge.svg)](https://github.com/izaksuilov/LightReactive.Avalonia/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A lightweight reactive core for Avalonia applications. It replaces ReactiveUI, System.Reactive and DynamicData for the part that view models actually need: notifying properties, property observation, subjects and operators, validation, commands, a collection with batch operations and a UI thread helper.

ReactiveUI and System.Reactive allocate a lot of memory (ConditionalWeakTable, schedulers, a wrapper per subscription). When there are thousands of view models and they are cloned often, this puts noticeable pressure on the garbage collector. This library contains only what is really used, without the heavy infrastructure.

The package consists of two parts:

- `LightReactive.Avalonia`: the library (net10.0, depends on the `Avalonia` package);
- `LightReactive.Avalonia.Generator`: a source generator for the `[Reactive]` attribute (netstandard2.0). It is not published as a separate package; it ships inside the `LightReactive.Avalonia` package, in `analyzers/dotnet/cs`.

---

## Contents

| Namespace | What is inside |
|---|---|
| `LightReactive.Avalonia` | `ReactiveObject` (view model base, `RaiseAndSetIfChanged`), the `[Reactive]` attribute, `Subject<T>`, `BehaviorSubject<T>`, `SubjectBase<T>`, `Unit`, `AnonymousObserver<T>`, `AnonymousObservable<T>`, `EventPattern<T>`, `Disposable`, `CompositeDisposable`, `SerialDisposable`, `SingleAssignmentDisposable`, `BooleanDisposable`, `DisposeWith`, `IScheduler`, `RxSchedulers` (`MainThreadScheduler`, `MainThreadInlineScheduler`), `WhenAnyValue` |
| `LightReactive.Avalonia.Linq` | `Observable` (`Return`, `Empty`, `Never`, `Create`, `FromEventPattern`) and extension operators: `Select`, `Where`, `Do`, `Skip`, `Take`, `StartWith`, `DistinctUntilChanged`, `Merge`, `CombineLatest`, `Switch`, `Throttle`, `ObserveOn`, `Subscribe` |
| `LightReactive.Avalonia.Validation` | `ReactiveValidationObject` (a base with `INotifyDataErrorInfo`), `IValidatableViewModel`, `ValidationContext`, `ValidationHelper`, the `ValidationRule` and `ClearValidationRules` extension methods |
| `LightReactive.Avalonia.Commands` | `ICommand<TParam, TResult>`, `IAsyncCommand<TParam, TResult>`, `ICommandDisposable`, the `ReactiveCommand<TParam, TResult>` and `ReactiveCommandAsync<TParam, TResult>` implementations, the `CommandHelper` factory |
| `LightReactive.Avalonia.Collections` | `ObservableCollectionExtended<T>` (`AddRange`, `RemoveMany`, `Load` with a single Reset notification), `AddRange` and `RemoveMany` extensions for `ICollection<T>` |
| `LightReactive.Avalonia.Threading` | `DispatcherHelper`: UI thread check, `Invoke`, `InvokeAsync`, `BeginInvoke`, dispatcher timers |

---

## Requirements

- .NET 10 and C# 14 in any project that uses `[Reactive]`: the code produced by the generator uses the `field` keyword.
- Avalonia 12: the UI thread scheduler and `CanExecuteChanged` notifications go through the Avalonia dispatcher.
- The generator is built with `Microsoft.CodeAnalysis.CSharp` 5.9.0. The consumer's compiler must be at least as new, otherwise the generator will not load (warning CS9057) and partial properties will stay without an implementation.

---

## Installation

### From NuGet

```xml
<PackageReference Include="LightReactive.Avalonia" Version="1.0.0" />
```

The `[Reactive]` generator is wired up automatically: it is part of the package as an analyzer.

### From source (ProjectReference)

Analyzers are not passed on transitively through a project reference. So the library is referenced once, and the generator is referenced separately in every project that uses `[Reactive]`:

```xml
<ProjectReference Include="..\LightReactive.Avalonia\src\LightReactive.Avalonia\LightReactive.Avalonia.csproj" />
<ProjectReference Include="..\LightReactive.Avalonia\src\LightReactive.Avalonia.Generator\LightReactive.Avalonia.Generator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

The generator creates the `[Reactive]` attribute itself, as `internal`, in every assembly it is attached to. So the generator can be used without the library if your model has its own base with a `RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)` method: the generator calls it with two arguments.

---

## Notifying properties

```csharp
using LightReactive.Avalonia;

public partial class PersonViewModel : ReactiveObject
{
    [Reactive] public partial string Name { get; set; } = string.Empty;

    [Reactive] public partial int Age { get; private set; }
}
```

The generator produces the implementation `get => field; set => RaiseAndSetIfChanged(ref field, value);`. You do not need to declare a backing field.

Requirements:

- the class and all its containing classes are `partial`, and so is the property;
- the property is an instance property with a `set` or `init` accessor: no code is generated for static properties and indexers;
- the base class has `RaiseAndSetIfChanged`: either `ReactiveObject` or your own base.

Property and accessor modifiers (access, including the default one, `required`, `new`) are copied from the declaration as is: both parts of a partial property must match.

If the setter needs logic, write the property by hand with `RaiseAndSetIfChanged(ref _x, value)`. Do not convert to `[Reactive]`: a property whose field has its own attributes (they would be lost), `virtual`, `override` and `static` properties, and a field that is needed elsewhere.

A pitfall of hand-written setters: the check `if (field == value) return;` suppresses a side effect when the default value is assigned (`default(enum)`), because a new object's field already equals it. Put the side effect after `RaiseAndSetIfChanged`.

---

## Observing properties

```csharp
using LightReactive.Avalonia;
using LightReactive.Avalonia.Linq;

this.WhenAnyValue(x => x.Name)
    .Throttle(TimeSpan.FromMilliseconds(300), RxSchedulers.MainThreadScheduler)
    .Subscribe(name => Search(name))
    .DisposeWith(_disposables);
```

Several properties:

```csharp
// A tuple of values, as in ReactiveUI: one value on subscription, then one on every change of any property
this.WhenAnyValue(x => x.MinX, x => x.MaxX)
    .Subscribe(range => UpdateAxis(range.Item1, range.Item2));

// With a selector: the computed value is emitted immediately
this.WhenAnyValue(x => x.HasErrors, x => x.IsBusy, (errors, busy) => !errors && !busy);
```

- A call with two to seven properties and no selector emits a tuple of values. A call with eight or more properties and no selector emits `Unit`: one signal per property on subscription, then one signal per change.

Repeated values are filtered out, as in ReactiveUI. A `PropertyChanged` notification without a change of value emits nothing: `RaisePropertyChanged` with the same value, a notification for all properties (`null` or an empty name), or replacing an intermediate link with an object that has the same value. Values are compared with `EqualityComparer<T>.Default` against the last emitted one. `DistinctUntilChanged()` is not needed after `WhenAnyValue` without a selector.

- With several properties, repeats are filtered per property. The selector's result is not compared, as in ReactiveUI: if different property values produce the same result, it is emitted again. If such a repeat is a problem, `DistinctUntilChanged()` after the selector is still needed. A tuple without a selector never repeats: it changes together with a property.
- The `isDistinct: false` parameter restores emission on every notification. It is needed, for example, when a collection in a property is modified in place and `RaisePropertyChanged` is sent so that subscribers re-read it:

  ```csharp
  this.WhenAnyValue(x => x.Items, isDistinct: false);
  this.WhenAnyValue(x => x.Filter, x => x.Items, (filter, items) => Apply(filter, items), isDistinct: false);
  // Unit for eight or more properties: the parameter goes first, it cannot be placed after params
  this.WhenAnyValue(false, x => x.A, x => x.B, x => x.C, x => x.D, x => x.E, x => x.F, x => x.G, x => x.H);
  ```

- A property validation rule (`ValidationRule(x => x.Name, ...)`) observes the property through `WhenAnyValue`, so a notification without a change of value does not re-check it either, as in ReactiveUI.Validation. If a rule depends on other state, define it on the stream of that state: `ValidationRule(x => x.Name, this.WhenAnyValue(x => x.Name, x => x.Other), ...)`.

Differences from ReactiveUI:

- If an intermediate link of the path `x => x.A.B` is null, `default` is emitted: immediately on subscription, then, like any value, only if it differs from the previous one. ReactiveUI emitted nothing in this case.
- `RxSchedulers.MainThreadScheduler` always queues the action on the dispatcher, even from the UI thread. The Avalonia.ReactiveUI scheduler ran such an action immediately. `RxSchedulers.MainThreadInlineScheduler` provides that behavior: from the UI thread an action without delay runs in the same call (up to 32 nested calls, deeper ones go through the queue); from other threads and with a delay it is queued. It is needed where code relies on `ObserveOn` on the UI thread delivering the value immediately. For example, a command state must be updated before the end of a view model constructor.

Deliberate differences from System.Reactive:

- notifications are delivered to a snapshot of subscribers, and callbacks are invoked outside the lock;
- an exception in an operator delegate does not propagate into the code that changed the property: the operator sends it to `OnError`. As in System.Reactive, `OnError` and `OnCompleted` terminate the sequence: the subscriber gets nothing afterwards, and subscriptions to the sources are released;
- after `Dispose` a subject silently ignores notifications instead of throwing;
- `ObserveOn` cancels deliveries that have not run yet when unsubscribed. Notification order is preserved with any scheduler: with `MainThreadInlineScheduler` a value from the UI thread does not overtake an earlier one still waiting in the dispatcher queue.

Known limitations, left in place deliberately:

- `BehaviorSubject.Subscribe` delivers the initial value outside the lock: with a concurrent `OnNext` the new value may arrive before the initial one. Fixing it requires holding the lock during the callback and leads to deadlocks, while consumers are single-threaded.
- Unsubscribing from `Throttle` stops the dispatcher timer from the thread that unsubscribed. `DispatcherTimer` is not thread-safe: re-check this with a background source.

---

## Commands

```csharp
using LightReactive.Avalonia;
using LightReactive.Avalonia.Commands;

SaveCommand = CommandHelper.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.CanSave));
DeleteCommand = CommandHelper.Create<Item>(Delete);
```

- Use only `CreateFromTask` for an asynchronous method. `Create` with an asynchronous method is a build error: otherwise the exception would be lost.
- An `async void` lambda converts to `Action`, so `Create` silently compiles and loses exceptions. The compiler cannot catch this.
- Bindings in markup are string-based. If `CommandParameter` is not null and cannot be cast to the command's parameter type, an `InvalidOperationException` is thrown on the first execution. When changing a command signature, check `CommandParameter` in the `.axaml`.
- `CanExecuteChanged` is always raised on the UI thread.

---

## Validation

```csharp
using LightReactive.Avalonia;
using LightReactive.Avalonia.Validation;

public partial class LoginViewModel : ReactiveValidationObject
{
    [Reactive] public partial string User { get; set; } = string.Empty;

    public LoginViewModel()
    {
        this.ValidationRule(x => x.User, user => !string.IsNullOrWhiteSpace(user), "Enter a user name");
    }
}
```

`ReactiveValidationObject` implements `INotifyDataErrorInfo`, so errors are visible to Avalonia bindings. The overall flag is `ValidationContext.IsValid`.

Removing rules, as in ReactiveUI.Validation:

```csharp
this.ClearValidationRules();              // all rules of the model
this.ClearValidationRules(x => x.User);   // rules of a single property
```

- A removed rule unsubscribes from its source. The context stays usable: new rules are added as usual.
- Subscriptions to `ValidationContext.Valid` are preserved. If a model recreates its rules by calling `ClearValidationRules` (for example, when the language changes), subscribe to `Valid` once, not on every recreation.
- Rules of a property are found by the name of the last link of the path: `x => x.A.B` removes the rules of property `B`. Rules created on an arbitrary stream without a property are removed only by the call without arguments.
- `ValidationHelper.Dispose()` after `ClearValidationRules` does nothing.

Error events are raised only on a real change:

- `ErrorsChanged` for a property: a rule was added or removed while it is invalid, or a rule's result or message changed.
- Notification about `HasErrors`, as well as `ValidationContext.IsValid` and `ValidationContext.Valid`: only when the overall result changes.
- A valid rule raises no events when added or removed. Changing the message of an invalid rule raises `ErrorsChanged` without `HasErrors`. A second invalid rule on an already invalid model also raises only `ErrorsChanged`.
- A removed rule raises no events, even if its source fired on another thread at the same moment: the context checks that the rule is still in it under the same lock that is used to remove rules.

The validation context is lazy:

- The context is created on the first access to `ValidationContext`, usually with the first rule. `HasErrors`, `GetErrors` and `ClearValidationRules` do not create it, so a model without rules has no context at all. Check a model for errors through `HasErrors` rather than `ValidationContext.GetIsValid()`: that way no context is created just for one read.
- The first access from several threads yields the same context. The model subscribes to it before other threads can see it, so a rule added right after creation already reports the error to the model.
- After `Dispose` no new context is created. If a context existed, it is disposed and stays with the model as before: `HasErrors` keeps the last result, rules are unsubscribed, and there are no more events. If there was no context, the model returns a shared disposed context.
- A disposed context does not accept rules: a rule added after `Dispose` is unsubscribed from its source immediately. Subscribing to `Valid` of a disposed context throws `ObjectDisposedException`, and it does not remember new `PropertyChanged` subscribers (through `INotifyPropertyChanged`).
- The `Valid` stream is also created on first access.

---

## Tests

```
dotnet test LightReactive.Avalonia.slnx
```

`tests/LightReactive.Avalonia.Tests`:

- `ReactiveCoreTests`: subjects, subscription containers, property observation, operator errors;
- `OperatorTests`: operators and factories of `LightReactive.Avalonia.Linq`, completion according to the Rx contract, a manual scheduler for `Throttle` and `ObserveOn`;
- `ObservableCollectionExtendedTests`: collection batch operations and the Reset notification;
- `SchedulerTests`: UI thread schedulers and `DispatcherHelper` timers; the Avalonia dispatcher runs on a separate test thread (`TestUiThread`);
- `CommandTests`: command `CanExecute` and `CanExecuteChanged` on the UI thread;
- `ValidationTests`: removing validation rules and re-checking a property rule;
- `ValidationEventsTests`: `ErrorsChanged`, `HasErrors`, `IsValid` and `Valid` events only on a real change of errors; a removed rule raises no events;
- `ValidationContextLazyTests`: the lazy validation context, a race on first access from several threads, disposing the model, removing rules on a custom `IValidatableViewModel` implementation;
- `ReactiveGeneratorTests`: running the generator through `GeneratorDriver`, checking the generated code and its compilation;
- `ReactivePropertyTests`: `[Reactive]` properties in live code with the generator attached as an analyzer.

---

## License

MIT, see [LICENSE](LICENSE).
