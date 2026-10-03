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
        element.Margins = new Margins(1f, 2f, 3f, 4f);
        element.Height = 12f;
        element.Width = 24f;
        element.Bounds = new Rectangle<float>(1f, 2f, 3f, 4f);
        element.Margins = new Margins(1f, 2f, 3f, 4f);

        Assert.Equal(
            [
                nameof(Element.Height),
                nameof(Element.Width),
                nameof(Element.Bounds),
                nameof(Element.Margins),
            ],
            changedProperties
        );
    }

    /// <summary>Verifies margins default to zero and inset measured and arranged content.</summary>
    [Fact]
    public void Margins_defaultToZeroAndInsetContent()
    {
        var element = new Element();
        var allocation = new Rectangle<float>(10f, 20f, 30f, 40f);

        Assert.Equal(default, element.Margins);
        Assert.Equal(new Vector2D<float>(30f, 40f), element.Measure(allocation.Size));

        element.Margins = new Margins(3f, 4f, 5f, 6f);
        element.Arrange(allocation);

        Assert.Equal(new Rectangle<float>(13f, 25f, 23f, 29f), element.Bounds);
    }

    /// <summary>Verifies invalid negative or non-finite margins are rejected.</summary>
    [Fact]
    public void Margins_rejectInvalidValues()
    {
        var element = new Element();

        Assert.Throws<ArgumentOutOfRangeException>(() => element.Margins = new(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => element.Margins = new(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => element.Margins = new(float.PositiveInfinity));
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
