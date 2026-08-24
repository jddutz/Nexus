namespace Nexus.Graphics;

/// <summary>
/// Opaque handle to a backend-specific GPU resource (buffer, descriptor set, image, pipeline, etc.).
/// Backends (e.g. Nexus.Graphics.Vulkan) map this to their native handle type internally.
/// </summary>
public readonly record struct GpuHandle(ulong Value)
{
    /// <summary>Represents an unset/invalid handle.</summary>
    public static readonly GpuHandle Invalid = new(0);

    /// <summary>Gets whether this handle refers to a real resource.</summary>
    public bool IsValid => Value != 0;
}
