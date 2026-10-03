using Nexus.GUI;

namespace Tests;

/// <summary>Tests item spacing configuration.</summary>
public class ItemSpacingTests
{
    /// <summary>Verifies default construction and <see cref="ItemSpacing.None"/> have no spacing.</summary>
    [Fact]
    public void DefaultSpacing_isNone()
    {
        Assert.Equal(new ItemSpacing(), ItemSpacing.None);
        Assert.Equal(0f, ItemSpacing.None.Horizontal);
        Assert.Equal(0f, ItemSpacing.None.Vertical);
    }

    /// <summary>Verifies named optional arguments set the requested spacing values.</summary>
    [Fact]
    public void Constructor_acceptsNamedOptionalSpacing()
    {
        var spacing = new ItemSpacing(horizontal: 4f, vertical: 8f);

        Assert.Equal(4f, spacing.Horizontal);
        Assert.Equal(8f, spacing.Vertical);
    }

    /// <summary>Verifies negative and non-finite spacing values are rejected.</summary>
    [Fact]
    public void Constructor_rejectsInvalidSpacing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSpacing(horizontal: -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemSpacing(vertical: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ItemSpacing(horizontal: float.PositiveInfinity)
        );
    }
}
