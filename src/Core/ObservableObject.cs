namespace Nexus.Core;

/// <summary>
/// Provides a conventional <see cref="INotifyPropertyChanged"/> base, with a
/// <see cref="SetProperty{T}"/> helper for backing-field mutators, shared by
/// <see cref="GameObject"/> and <see cref="Component"/>.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises
    /// <see cref="PropertyChanged"/> when the new value differs from the current one.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="field">A reference to the backing field.</param>
    /// <param name="value">The new value to assign.</param>
    /// <param name="propertyName">The name of the property, supplied automatically by the compiler.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null
    )
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property, supplied automatically by the compiler.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
