namespace Nexus.GUI.Elements;

/// <summary>A full-screen dimmer with a centered content container. Dispose to close.</summary>
public sealed class ModalDialog : ImageElement, IDisposable
{
    private Action? _close;
    internal ModalDialog(Action close)
    {
        _close = close;
        Texture = BuiltInTextures.Uniform;
        Color = new Color(0f, 0f, 0f, 0.65f);
        SizingMode = ImageSizingMode.Stretch;
        RenderLayerMask = RenderLayers.DefaultUI;
        SortOrder = 10000;
        Children.Add(Content);
    }

    /// <summary>Populate this centered container with the dialog's layout and controls.</summary>
    public Element Content { get; } = new() { Width = 400f, Height = 240f };

    internal bool ContainsMap(InputMap map) => Enumerate(Content).Prepend(this).Any(element => ReferenceEquals(element.InputMap, map));
    internal bool ContainsElement(Element element) => Enumerate(Content).Prepend(this).Contains(element);

    private static IEnumerable<Element> Enumerate(IGameObject root)
    {
        if (root is Element element) yield return element;
        foreach (var child in root.Children.OfType<IGameObject>())
        foreach (var descendant in Enumerate(child)) yield return descendant;
    }

    public override void Arrange(Rectangle<float> bounds)
    {
        var order = SortOrder + 2;
        foreach (var element in Enumerate(Content))
        {
            element.SortOrder = order;
            order += 2;
        }
        base.Arrange(bounds);
    }

    /// <summary>Closes the dialog and restores scene input.</summary>
    public void Dispose()
    {
        var close = _close;
        _close = null;
        close?.Invoke();
    }
}
