# Nexus code generation

This directory contains the Roslyn source generator and analyzer for Nexus observable properties. `Nexus.SourceGenerators.csproj` targets `netstandard2.0` and is consumed as an analyzer by engine projects.

This document records the agreed observable pattern.

## Eligible types and notification ownership

The generator creates partial classes for classes implementing `Nexus.Core.IObservable`, including classes inheriting an implementation. Classes outside that hierarchy must not receive observable-property generation. Authored classes and their containing types must be partial where required to extend them with generated members.

`IObservable.PropertyChanged` uses `Action<string>`. The event must be authored explicitly; the generator must never supply a missing event. A class responsible for implementing the interface must declare the event, leaving a missing implementation to ordinary compiler diagnostics. A derived class may inherit the event and its notification method rather than redeclaring the event.

The generator supplies a protected, nonvirtual notification method in the class declaring the event:

```csharp
protected void NotifyPropertyChanged(string propertyName)
    => PropertyChanged?.Invoke(propertyName);
```

Derived classes use this inherited method to raise property notifications. They do not invoke an inherited event directly or introduce a separate generated event. The generated wrapper uses `NotifyPropertyChanged` for property notifications.

## Generated property and mutation methods

For each `[Observable]` backing field, generate:

- A public getter.
- A nonvirtual property setter: public when `ObservableAttribute.PublicSetter` is `true`, otherwise protected.
- The `required` modifier when `ObservableAttribute.Required` is `true`.
- A private `__SetPropertyName` wrapper called by the property setter.
- A protected virtual `SetPropertyName(T value)` mutation method, unless the authored type provides a compatible protected setter.
- Two protected virtual after-change hooks.

`PublicSetter` controls the property setter's accessibility, not the accessibility of `SetPropertyName`.

The wrapper performs these steps in order:

1. Compare the proposed value with the backing field using `EqualityComparer<T>.Default`; return when equal.
2. Capture the previous backing-field value.
3. Call `SetPropertyName(value)`.
4. Compare the backing field with the previous value; return if no change occurred.
5. Capture the assigned value for the typed change event.
6. Call `AfterPropertyNameChanges()`.
7. Call `AfterPropertyNameChanges(previousValue)`.
8. Call `NotifyPropertyChanged(nameof(PropertyName))`.
9. Raise the typed change event when enabled.

The default `SetPropertyName` implementation assigns the backing field. An authored protected `SetPropertyName(T value)` method replaces that generated default and controls assignment. This is the pre-assignment validation point: validate the incoming value before assigning the observable backing field. The private wrapper owns change detection, after-change hooks, and notifications; direct calls to `SetPropertyName` perform mutation without that wrapper. Use property assignment when the full change pipeline is required.

For example, a property that must reject invalid values before changing state can provide:

```csharp
[Nexus.Core.Observable]
private Margins _margins;

protected virtual void SetMargins(Margins value)
{
    ValidateMargins(value);
    _margins = value;
}
```

The generator recognizes this protected setter, leaves its implementation intact, and still generates `__SetMargins` to perform comparisons, invoke after-change hooks, raise `PropertyChanged`, and raise the typed change event. The analyzer permits intentional backing-field access inside the matching custom setter.

Capturing the assigned value before callbacks ensures that the typed event describes this assignment even if a callback performs another assignment.

## After-change hooks

Each property has two independently handled overloads:

```csharp
protected virtual partial void AfterPropertyNameChanges();
protected virtual partial void AfterPropertyNameChanges(T previousValue);
```

When the author supplies a partial implementation for an overload, generate its matching partial declaration. When no implementation exists, generate a normal protected virtual no-op method for that overload. Do not emit a partial declaration without an implementation: its accessibility and virtual modifier require a body and would otherwise cause a compiler error.

For example, with neither overload authored:

```csharp
protected virtual void AfterCountChanges() { }
protected virtual void AfterCountChanges(int previousValue) { }
```

Derived classes may override either or both overloads. The wrapper always calls the parameterless overload first, followed by the previous-value overload.

## Example generated shape

Given an authored event and field:

```csharp
public partial class Counter : Nexus.Core.IObservable
{
    public event Action<string>? PropertyChanged;

    [Nexus.Core.Observable]
    private int _count;
}
```

The generated shape is:

```csharp
public partial class Counter
{
    protected void NotifyPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(propertyName);

    public int Count
    {
        get => _count;
        set => __SetCount(value);
    }

    public event Action<int, int>? CountChanged;

    private void __SetCount(int value)
    {
        if (EqualityComparer<int>.Default.Equals(_count, value))
            return;

        var previousValue = _count;
        SetCount(value);

        if (EqualityComparer<int>.Default.Equals(_count, previousValue))
            return;

        var assignedValue = _count;
        AfterCountChanges();
        AfterCountChanges(previousValue);
        NotifyPropertyChanged(nameof(Count));
        CountChanged?.Invoke(previousValue, assignedValue);
    }

    protected virtual void SetCount(int value) => _count = value;
    protected virtual void AfterCountChanges() { }
    protected virtual void AfterCountChanges(int previousValue) { }
}
```

The example assumes the usual `System` and `System.Collections.Generic` imports. With `PublicSetter = false`, only the property setter changes to `protected set`. With `GenerateChangedEvent = false`, omit the typed event and its invocation; `PropertyChanged` notifications still occur.

## Analyzer expectations

The analyzer warns on direct reads and writes of observable backing fields outside their matching custom `SetPropertyName` mutation method. Intentional assignment inside that setter is allowed because it is the generator's documented pre-validation extension point. Event signatures must adhere to `IObservable`'s `Action<string>` contract.

After-change hooks run only after the custom setter has accepted and assigned the value. Component-specific modification notifications remain separately owned.

## Implementation status and validation

The generator emits properties only for classes implementing `IObservable` directly or through a base class. It relies on the authored `PropertyChanged` event, supplies a protected nonvirtual `NotifyPropertyChanged` bridge in the event-declaring class, and uses a private change-detection wrapper around each protected virtual mutation method. It generates no-op after-change hooks when no implementation is authored. The analyzer validates event signatures and warns about direct backing-field access.

From the repository root:

```sh
dotnet test tests/SourceGeneratorTests/Nexus.SourceGeneratorTests.csproj
```

See [source-generator tests](../tests/SourceGeneratorTests), [Core contracts](../src/Core/README.md), and the [repository architecture baseline](../README.md).
