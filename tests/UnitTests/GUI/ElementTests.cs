using Nexus.GUI;
using Silk.NET.Maths;

namespace Tests;

/// <summary>
/// Tests property-change notifications for GUI elements.
/// </summary>
public class ElementTests
{
    /// <summary>
    /// Verifies layout property changes notify listeners once and ignore repeated values.
    /// </summary>
    [Fact]
    public void LayoutProperties_raisePropertyChangedOnlyWhenValuesChange()
    {
        var element = new Element();
        var changedProperties = new List<string?>();
        element.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        element.Height = 12f;
        element.Width = 24f;
        element.Bounds = new Rectangle<float>(1f, 2f, 3f, 4f);
        element.Height = 12f;
        element.Width = 24f;
        element.Bounds = new Rectangle<float>(1f, 2f, 3f, 4f);

        Assert.Equal(
            [nameof(Element.Height), nameof(Element.Width), nameof(Element.Bounds)],
            changedProperties
        );
    }
}
