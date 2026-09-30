# Nexus Code Generation

```csharp
public partial class GameObject2D : IObservable
{
    [Observable]
    private Matrix4X4<float> _worldTransform = Matrix4x4<float>.Identity;

    protected virtual bool ValidateWorldTransform(Matrix4X4<float> value) => true;
    protected virtual void BeforeWorldTransformChanges(Matrix4X4<float> newValue) { }
    protected virtual void AfterWorldTransformChanges(Matrix4X4<float> previousValue) { }

    public event EventHandler? Modified;
}

// Generated:
public partial class GameObject2D
{
    public event Action<string>? PropertyChanged;
    public Matrix4X4<float> WorldTransform => _worldTransform;

    public event Action<Matrix4X4<float>, Matrix4x4<float>>? WorldTransformChanged;

    public virtual void SetWorldTransform(Matrix4x4<float> value)
    {
        if (EqualityComparer<Matrix4X4<float>>.Default.Equals(_worldTransform, value))
            return;

        if (!ValidateWorldTransform(value))
            return;

        var previousValue = _worldTransform;
        BeforeWorldTransformChanges(value);
        _worldTransform = value;
        var assignedValue = _worldTransform;

        AfterWorldTransformChanges(previousValue);

        PropertyChanged?.Invoke(nameof(WorldTransform));
        WorldTransformChanged?.Invoke(previousValue, assignedValue);
    }
}
```

[Observable] applies to mutable instance fields in partial classes. A single leading underscore is
removed and the first remaining character is uppercased; pass `[Observable("Name")]` to override
the derived name. All containing types must also be partial. Generated properties have public
getters and no property setters; mutation goes through the generated public virtual `SetName` method.

The generator calls optional `ValidateName(T value)`, `BeforeNameChanges(T newValue)`, and
`AfterNameChanges(T previousValue)` methods only when matching accessible methods are authored. It
does not generate empty hook methods. Equal assignments skip validation and notifications. Rejected
values do not mutate the field. Events are raised after assignment, and the typed change event
reports the value assigned by that call even when callbacks make a later reentrant assignment.

For a type implementing `IObservable`, the generator adds `PropertyChanged` only if the type does
not already supply one. An inherited event must have an accessible `OnPropertyChanged(string)`
method for generated setters to raise it. Types that do not implement `IObservable` receive only
the typed change event. `IComponent.Modified` remains manually owned; generated property changes
do not raise it.