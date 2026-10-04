namespace Nexus.Core;

/// <summary>
/// Marks an instance field for generation of an observable property and change notification.
/// </summary>
/// <param name="propertyName">An optional property name that overrides field-name derivation.</param>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ObservableAttribute(string? propertyName = null) : Attribute
{
    /// <summary>
    /// Gets the property name override, or <see langword="null"/> to derive it from the field name.
    /// </summary>
    public string? PropertyName { get; } = propertyName;

    /// <summary>
    /// Gets or sets whether the generated property setter is public.
    /// </summary>
    public bool PublicSetter { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a typed property-changed event is generated.
    /// </summary>
    public bool GenerateChangedEvent { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the generated property must be assigned during object initialization.
    /// </summary>
    public bool Required { get; set; }
}
