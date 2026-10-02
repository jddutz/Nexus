using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>
/// Tests property-change notifications for GUI elements.
/// </summary>
public class ElementTests
{
    /// <summary>Verifies element visibility and enabled state default to true.</summary>
    [Fact]
    public void InteractionProperties_defaultToTrue()
    {
        var element = new Element();

        Assert.True(element.IsVisible);
        Assert.True(element.IsEnabled);
    }

    /// <summary>
    /// Verifies layout property changes notify listeners once and ignore repeated values.
    /// </summary>
    [Fact]
    public void LayoutProperties_raisePropertyChangedOnlyWhenValuesChange()
    {
        var element = new Element();
        var changedProperties = new List<string?>();
        element.PropertyChanged += propertyName => changedProperties.Add(propertyName);

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

    /// <summary>Verifies hidden elements expose zero bounds until the next layout pass.</summary>
    [Fact]
    public void Visibility_collapsesBoundsUntilRearranged()
    {
        var bounds = new Rectangle<float>(1f, 2f, 3f, 4f);
        var element = new Element { Bounds = bounds };

        element.IsVisible = false;
        Assert.Equal(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero), element.Bounds);
        element.IsVisible = true;
        Assert.Equal(Vector2D<float>.Zero, element.Bounds.Size);
        element.Arrange(bounds);
        Assert.Equal(bounds, element.Bounds);

        var parent = new Element();
        parent.AddChild(element);
        parent.IsVisible = false;
        Assert.Equal(bounds, element.Bounds);
        element.Arrange(bounds);
        Assert.Equal(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero), element.Bounds);
        parent.IsVisible = true;
        Assert.Equal(Vector2D<float>.Zero, element.Bounds.Size);
        element.Arrange(bounds);
        Assert.Equal(bounds, element.Bounds);
    }
}
