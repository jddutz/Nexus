namespace Nexus.Graphics.Text;

/// <summary>
/// Defines built-in font identifiers and provides their embedded decoded outlines.
/// </summary>
public static partial class BuiltInFonts
{
    /// <summary>Gets the default font content identifier.</summary>
    public static ContentId Default => Regular;

    /// <summary>Gets the Aileron Black font content identifier.</summary>
    public static readonly ContentId Black = "nexus.default.black";

    /// <summary>Gets the Aileron Black Italic font content identifier.</summary>
    public static readonly ContentId BlackItalic = "nexus.default.black-italic";

    /// <summary>Gets the Aileron Bold font content identifier.</summary>
    public static readonly ContentId Bold = "nexus.default.bold";

    /// <summary>Gets the Aileron Bold Italic font content identifier.</summary>
    public static readonly ContentId BoldItalic = "nexus.default.bold-italic";

    /// <summary>Gets the Aileron Heavy font content identifier.</summary>
    public static readonly ContentId Heavy = "nexus.default.heavy";

    /// <summary>Gets the Aileron Heavy Italic font content identifier.</summary>
    public static readonly ContentId HeavyItalic = "nexus.default.heavy-italic";

    /// <summary>Gets the Aileron Italic font content identifier.</summary>
    public static readonly ContentId Italic = "nexus.default.italic";

    /// <summary>Gets the Aileron Light font content identifier.</summary>
    public static readonly ContentId Light = "nexus.default.light";

    /// <summary>Gets the Aileron Light Italic font content identifier.</summary>
    public static readonly ContentId LightItalic = "nexus.default.light-italic";

    /// <summary>Gets the Aileron Regular font content identifier.</summary>
    public static readonly ContentId Regular = "nexus.default.regular";

    /// <summary>Gets the Aileron SemiBold font content identifier.</summary>
    public static readonly ContentId SemiBold = "nexus.default.semi-bold";

    /// <summary>Gets the Aileron SemiBold Italic font content identifier.</summary>
    public static readonly ContentId SemiBoldItalic = "nexus.default.semi-bold-italic";

    /// <summary>Gets the Aileron Thin font content identifier.</summary>
    public static readonly ContentId Thin = "nexus.default.thin";

    /// <summary>Gets the Aileron Thin Italic font content identifier.</summary>
    public static readonly ContentId ThinItalic = "nexus.default.thin-italic";

    /// <summary>Gets the Aileron UltraLight font content identifier.</summary>
    public static readonly ContentId UltraLight = "nexus.default.ultra-light";

    /// <summary>Gets the Aileron UltraLight Italic font content identifier.</summary>
    public static readonly ContentId UltraLightItalic = "nexus.default.ultra-light-italic";

    /// <summary>Gets all built-in font content identifiers.</summary>
    public static readonly IReadOnlyList<ContentId> All = Array.AsReadOnly(
        new[]
        {
            Black,
            BlackItalic,
            Bold,
            BoldItalic,
            Heavy,
            HeavyItalic,
            Italic,
            Light,
            LightItalic,
            Regular,
            SemiBold,
            SemiBoldItalic,
            Thin,
            ThinItalic,
            UltraLight,
            UltraLightItalic,
        }
    );
}
