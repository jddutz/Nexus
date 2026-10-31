using Nexus.Graphics;
using Nexus.Graphics.Components;

namespace Tests;

/// <summary>Records runtime-owned views registered by GUI tests.</summary>
internal sealed class TestGraphicsSystem : IGraphicsSystem
{
    /// <summary>Gets the views currently registered for rendering.</summary>
    public List<ViewRenderer> RegisteredViews { get; } = [];

    /// <inheritdoc />
    public void Initialize() { }

    /// <inheritdoc />
    public void Render() { }

    /// <inheritdoc />
    public void RegisterView(ViewRenderer view)
    {
        ArgumentNullException.ThrowIfNull(view);
        RegisteredViews.Add(view);
    }

    /// <inheritdoc />
    public void UnregisterView(ViewRenderer view)
    {
        ArgumentNullException.ThrowIfNull(view);
        RegisteredViews.Remove(view);
    }
}
