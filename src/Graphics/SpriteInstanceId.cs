namespace Nexus.Graphics;

/// <summary>Identifies a sprite independently of its packed GPU index.</summary>
public readonly record struct SpriteInstanceId(ulong Value)
{
    private static long _nextId;
    /// <summary>Creates a unique sprite instance ID.</summary>
    public static SpriteInstanceId New() => new((ulong)Interlocked.Increment(ref _nextId));
}
