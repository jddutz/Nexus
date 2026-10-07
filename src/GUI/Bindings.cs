namespace Nexus.GUI;

public enum BindingMode
{
    OneTime,
    OneWay,
    TwoWay,
}

public interface IBinding { }

public interface IBindingDefinition
{
    IBinding Attach(object target);
}

public sealed class BindingDefinition<TSource, TTarget, TValue> : IBindingDefinition
{
    public TSource Source { get; }

    public Expression<Func<TSource, TValue>> SourceProperty { get; }

    public Expression<Func<TTarget, TValue>> TargetProperty { get; }

    public BindingMode Mode { get; }

    public BindingDefinition(
        TSource source,
        Expression<Func<TSource, TValue>> sourceProperty,
        Expression<Func<TTarget, TValue>> targetProperty,
        BindingMode mode = BindingMode.OneWay
    )
    {
        Source = source;
        SourceProperty = sourceProperty;
        TargetProperty = targetProperty;
        Mode = mode;
    }

    public IBinding Attach(object target)
    {
        // Validate target type and property capabilities.
        // Copy the source value to the target.
        // Create subscriptions required by Mode.
        // Return the disposable connection.
        throw new NotImplementedException();
    }
}
