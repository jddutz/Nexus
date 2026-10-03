using Nexus.GUI;

namespace Tests;

/// <summary>Tests item sizing configuration.</summary>
public class ItemSizeTests
{
    /// <summary>Verifies default construction uses zero minimums and unbounded maximums.</summary>
    [Fact]
    public void DefaultConstructor_usesUnboundedDefaults()
    {
        var size = new ItemSize();

        Assert.Equal(0f, size.MinimumHeight);
        Assert.Equal(float.MaxValue, size.MaximumHeight);
        Assert.Equal(0f, size.MinimumWidth);
        Assert.Equal(float.MaxValue, size.MaximumWidth);
        Assert.False(size.MaintainAspectRatio);
    }

    /// <summary>Verifies named optional arguments set only the requested sizing values.</summary>
    [Fact]
    public void Constructor_acceptsNamedOptionalBounds()
    {
        var size = new ItemSize(minimumWidth: 10f, maximumWidth: 50f, maintainAspectRatio: true);

        Assert.Equal(0f, size.MinimumHeight);
        Assert.Equal(float.MaxValue, size.MaximumHeight);
        Assert.Equal(10f, size.MinimumWidth);
        Assert.Equal(50f, size.MaximumWidth);
        Assert.True(size.MaintainAspectRatio);
    }

    /// <summary>Verifies invalid or inverted bounds are rejected.</summary>
    [Fact]
    public void Constructor_rejectsInvalidBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSize(minimumHeight: -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSize(maximumHeight: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ItemSize(minimumWidth: 20f, maximumWidth: 10f)
        );
    }
}
