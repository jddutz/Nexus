namespace Tests;

using Nexus.Graphics;

/// <summary>
/// Tests value equality for colors.
/// </summary>
public class ColorTests
{
    /// <summary>
    /// Ensures colors with equal channels compare equal and produce equal hash codes.
    /// </summary>
    [Fact]
    public void Equal_channel_values_are_equal_and_have_matching_hash_codes()
    {
        var first = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        var second = new Color(0.1f, 0.2f, 0.3f, 0.4f);

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// Ensures a difference in any channel makes colors unequal.
    /// </summary>
    [Fact]
    public void Different_channel_values_are_not_equal()
    {
        var color = new Color(0.1f, 0.2f, 0.3f, 0.4f);

        Assert.NotEqual(color, new Color(0.5f, 0.2f, 0.3f, 0.4f));
        Assert.NotEqual(color, new Color(0.1f, 0.5f, 0.3f, 0.4f));
        Assert.NotEqual(color, new Color(0.1f, 0.2f, 0.5f, 0.4f));
        Assert.NotEqual(color, new Color(0.1f, 0.2f, 0.3f, 0.5f));
        Assert.True(color != new Color(0.1f, 0.2f, 0.3f, 0.5f));
        Assert.False(color == new Color(0.1f, 0.2f, 0.3f, 0.5f));
    }
}
