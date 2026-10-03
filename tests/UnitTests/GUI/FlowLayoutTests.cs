using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>Tests flow-layout measurement, arrangement, and invalidation.</summary>
public class FlowLayoutTests
{
    /// <summary>Verifies the default row layout consumes remaining primary capacity in order.</summary>
    [Fact]
    public void Arrange_defaultRowLayoutUsesRemainingPrimaryCapacity()
    {
        var layout = new FlowLayout();
        var first = Child(100f, 100f);
        var second = Child(100f, 100f);
        var third = Child(100f, 100f);
        layout.AddChild(first);
        layout.AddChild(second);
        layout.AddChild(third);

        layout.Arrange(new Rectangle<float>(0f, 0f, 115f, 100f));

        Assert.Equal(new Rectangle<float>(0f, 0f, 100f, 100f), first.Bounds);
        Assert.Equal(new Rectangle<float>(100f, 0f, 15f, 100f), second.Bounds);
        Assert.Equal(new Rectangle<float>(115f, 0f, 0f, 100f), third.Bounds);
    }

    /// <summary>Verifies column priority transposes primary and cross-axis allocation.</summary>
    [Fact]
    public void Arrange_columnPriorityStacksDownward()
    {
        var layout = new FlowLayout { Priority = LayoutPriority.Vertical };
        var first = Child(100f, 100f);
        var second = Child(100f, 100f);
        var third = Child(100f, 100f);
        layout.AddChild(first);
        layout.AddChild(second);
        layout.AddChild(third);

        layout.Arrange(new Rectangle<float>(0f, 0f, 100f, 115f));

        Assert.Equal(new Rectangle<float>(0f, 0f, 100f, 100f), first.Bounds);
        Assert.Equal(new Rectangle<float>(0f, 100f, 100f, 15f), second.Bounds);
        Assert.Equal(new Rectangle<float>(0f, 115f, 100f, 0f), third.Bounds);
    }

    /// <summary>Verifies fitted lanes reserve their inter-lane spacing.</summary>
    [Fact]
    public void Arrange_fitReservesCrossAxisGap()
    {
        var layout = new FlowLayout
        {
            Size = 2,
            ItemSpacing = new ItemSpacing(vertical: 10f),
        };
        var first = Child(80f, 80f);
        var second = Child(80f, 80f);
        layout.AddChild(first);
        layout.AddChild(second);

        layout.Arrange(new Rectangle<float>(0f, 0f, 100f, 100f));

        Assert.Equal(new Rectangle<float>(0f, 0f, 80f, 45f), first.Bounds);
        Assert.Equal(new Rectangle<float>(0f, 55f, 80f, 45f), second.Bounds);
    }

    /// <summary>Verifies fixed lines wrap and use the remaining final cross-axis capacity.</summary>
    [Fact]
    public void Arrange_fixedLinesWrap()
    {
        var layout = new FlowLayout
        {
            LayoutMode = LayoutSizingMode.Fixed,
            Size = 30,
            ItemSpacing = new ItemSpacing(vertical: 5f),
        };
        var first = Child(100f, 30f);
        var second = Child(100f, 30f);
        layout.AddChild(first);
        layout.AddChild(second);

        layout.Arrange(new Rectangle<float>(0f, 0f, 180f, 100f));

        Assert.Equal(new Rectangle<float>(0f, 0f, 100f, 30f), first.Bounds);
        Assert.Equal(new Rectangle<float>(0f, 35f, 100f, 30f), second.Bounds);
    }

    /// <summary>Verifies direction changes mirror placement without changing child order.</summary>
    [Fact]
    public void Arrange_reverseDirectionsMirrorPlacement()
    {
        var layout = new FlowLayout
        {
            LayoutMode = LayoutSizingMode.Fixed,
            Size = 30,
            HorizontalDirection = DirectionHorizontal.RightToLeft,
            VerticalDirection = DirectionVertical.Up,
        };
        var first = Child(80f, 30f);
        var second = Child(90f, 30f);
        layout.AddChild(first);
        layout.AddChild(second);

        layout.Arrange(new Rectangle<float>(0f, 0f, 180f, 100f));

        Assert.Equal(new Rectangle<float>(100f, 70f, 80f, 30f), first.Bounds);
        Assert.Equal(new Rectangle<float>(10f, 70f, 90f, 30f), second.Bounds);
    }

    /// <summary>Verifies flow-property changes notify the layout system.</summary>
    [Fact]
    public void LayoutProperties_invalidateLayout()
    {
        var layout = new FlowLayout();
        var notifications = new List<string>();
        layout.PropertyChanged += notifications.Add;

        layout.Priority = LayoutPriority.Vertical;
        layout.HorizontalDirection = DirectionHorizontal.RightToLeft;
        layout.VerticalDirection = DirectionVertical.Up;
        layout.LayoutMode = LayoutSizingMode.Fixed;
        layout.Size = 2;
        layout.ItemSize = new ItemSize(minimumWidth: 1f);
        layout.ItemSpacing = new ItemSpacing(horizontal: 2f);

        Assert.Equal(7, notifications.Count(name => name == string.Empty));
    }

    private static Element Child(float width, float height) =>
        new() { Width = width, Height = height };
}
