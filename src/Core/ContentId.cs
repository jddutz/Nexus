namespace Nexus.Core;

public readonly record struct ContentId(string Value)
{
    /// <summary>
    /// Creates a ContentId from a string value.
    /// </summary>
    public static implicit operator ContentId(string value) => new(value);

    /// <summary>
    /// Converts ContentId to its underlying string value.
    /// </summary>
    public static implicit operator string(ContentId id) => id.Value;

    public override string ToString() => Value;

    public static readonly ContentId Invalid = new(string.Empty);

    public static ContentId FromFilePath(string filepath)
    {
        return new ContentId(Path.GetFullPath(filepath));
    }
}
