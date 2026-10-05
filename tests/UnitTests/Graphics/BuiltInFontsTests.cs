using Nexus.Graphics.Text;

namespace Nexus.UnitTests.Graphics;

/// <summary>
/// Verifies the built-in font identifiers are unique and collectively listed.
/// </summary>
public sealed class BuiltInFontsTests
{
    /// <summary>
    /// Verifies every built-in font identifier is listed exactly once.
    /// </summary>
    [Fact]
    public void AllListsEveryBuiltInFontIdentifier()
    {
        Assert.Equal(16, BuiltInFonts.All.Count);
        Assert.Equal(BuiltInFonts.All.Count, BuiltInFonts.All.Distinct().Count());
        Assert.Contains(BuiltInFonts.Regular, BuiltInFonts.All);
        Assert.Contains(BuiltInFonts.UltraLightItalic, BuiltInFonts.All);
    }
}
