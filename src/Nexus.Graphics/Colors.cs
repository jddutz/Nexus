namespace Nexus.Graphics;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Color(float red, float green, float blue, float alpha = 1.0f)
{
    public readonly float R = red;
    public readonly float G = green;
    public readonly float B = blue;
    public readonly float A = alpha;
}

/// <summary>
/// Standard color constants as Color values (RGBA format).
/// Each component ranges from 0.0 to 1.0.
/// </summary>
public static class Colors
{
    // Helper method to convert ARGB hex to Color RGBA
    public static Color FromArgb(uint argb)
    {
        var a = ((argb >> 24) & 0xFF) / 255.0f;
        var r = ((argb >> 16) & 0xFF) / 255.0f;
        var g = ((argb >> 8) & 0xFF) / 255.0f;
        var b = (argb & 0xFF) / 255.0f;
        return new Color(r, g, b, a);
    }

    public static Color Lerp(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );
    }

    public static Color WithTransparency(this Color color, float a) =>
        new(color.R, color.G, color.B, a);

    // Standard named colors
    public static Color Transparent => FromArgb(0x00FFFFFF);
    public static Color AliceBlue => FromArgb(0xFFF0F8FF);
    public static Color AntiqueWhite => FromArgb(0xFFFAEBD7);
    public static Color Aqua => FromArgb(0xFF00FFFF);
    public static Color Aquamarine => FromArgb(0xFF7FFFD4);
    public static Color Azure => FromArgb(0xFFF0FFFF);
    public static Color Beige => FromArgb(0xFFF5F5DC);
    public static Color Bisque => FromArgb(0xFFFFE4C4);
    public static Color Black => FromArgb(0xFF000000);
    public static Color BlanchedAlmond => FromArgb(0xFFFFEBCD);
    public static Color Blue => FromArgb(0xFF0000FF);
    public static Color BlueViolet => FromArgb(0xFF8A2BE2);
    public static Color BrightGreen => FromArgb(0xFF00FF00);
    public static Color Brown => FromArgb(0xFFA52A2A);
    public static Color BurlyWood => FromArgb(0xFFDEB887);
    public static Color CadetBlue => FromArgb(0xFF5F9EA0);
    public static Color Chartreuse => FromArgb(0xFF7FFF00);
    public static Color Chocolate => FromArgb(0xFFD2691E);
    public static Color Coral => FromArgb(0xFFFF7F50);
    public static Color CornflowerBlue => FromArgb(0xFF6495ED);
    public static Color Cornsilk => FromArgb(0xFFFFF8DC);
    public static Color Crimson => FromArgb(0xFFDC143C);
    public static Color Cyan => FromArgb(0xFF00FFFF);
    public static Color DarkBlue => FromArgb(0xFF00008B);
    public static Color DarkCyan => FromArgb(0xFF008B8B);
    public static Color DarkGoldenrod => FromArgb(0xFFB8860B);
    public static Color DarkGray => FromArgb(0xFFA9A9A9);
    public static Color DarkGreen => FromArgb(0xFF006400);
    public static Color DarkKhaki => FromArgb(0xFFBDB76B);
    public static Color DarkMagenta => FromArgb(0xFF8B008B);
    public static Color DarkOliveGreen => FromArgb(0xFF556B2F);
    public static Color DarkOrange => FromArgb(0xFFFF8C00);
    public static Color DarkOrchid => FromArgb(0xFF9932CC);
    public static Color DarkRed => FromArgb(0xFF8B0000);
    public static Color DarkSalmon => FromArgb(0xFFE9967A);
    public static Color DarkSeaGreen => FromArgb(0xFF8FBC8B);
    public static Color DarkSlateBlue => FromArgb(0xFF483D8B);
    public static Color DarkSlateGray => FromArgb(0xFF2F4F4F);
    public static Color DarkTurquoise => FromArgb(0xFF00CED1);
    public static Color DarkViolet => FromArgb(0xFF9400D3);
    public static Color DeepPink => FromArgb(0xFFFF1493);
    public static Color DeepSkyBlue => FromArgb(0xFF00BFFF);
    public static Color DimGray => FromArgb(0xFF696969);
    public static Color DodgerBlue => FromArgb(0xFF1E90FF);
    public static Color Firebrick => FromArgb(0xFFB22222);
    public static Color FloralWhite => FromArgb(0xFFFFFAF0);
    public static Color ForestGreen => FromArgb(0xFF228B22);
    public static Color Fuchsia => FromArgb(0xFFFF00FF);
    public static Color Gainsboro => FromArgb(0xFFDCDCDC);
    public static Color GhostWhite => FromArgb(0xFFF8F8FF);
    public static Color Gold => FromArgb(0xFFFFD700);
    public static Color Goldenrod => FromArgb(0xFFDAA520);
    public static Color Gray => FromArgb(0xFF808080);
    public static Color Green => FromArgb(0xFF008000);
    public static Color GreenYellow => FromArgb(0xFFADFF2F);
    public static Color Honeydew => FromArgb(0xFFF0FFF0);
    public static Color HotPink => FromArgb(0xFFFF69B4);
    public static Color IndianRed => FromArgb(0xFFCD5C5C);
    public static Color Indigo => FromArgb(0xFF4B0082);
    public static Color Ivory => FromArgb(0xFFFFFFF0);
    public static Color Khaki => FromArgb(0xFFF0E68C);
    public static Color Lavender => FromArgb(0xFFE6E6FA);
    public static Color LavenderBlush => FromArgb(0xFFFFF0F5);
    public static Color LawnGreen => FromArgb(0xFF7CFC00);
    public static Color LemonChiffon => FromArgb(0xFFFFFACD);
    public static Color LightBlue => FromArgb(0xFFADD8E6);
    public static Color LightCoral => FromArgb(0xFFF08080);
    public static Color LightCyan => FromArgb(0xFFE0FFFF);
    public static Color LightGoldenrodYellow => FromArgb(0xFFFAFAD2);
    public static Color LightGray => FromArgb(0xFFD3D3D3);
    public static Color LightGreen => FromArgb(0xFF90EE90);
    public static Color LightPink => FromArgb(0xFFFFB6C1);
    public static Color LightSalmon => FromArgb(0xFFFFA07A);
    public static Color LightSeaGreen => FromArgb(0xFF20B2AA);
    public static Color LightSkyBlue => FromArgb(0xFF87CEFA);
    public static Color LightSlateGray => FromArgb(0xFF778899);
    public static Color LightSteelBlue => FromArgb(0xFFB0C4DE);
    public static Color LightYellow => FromArgb(0xFFFFFFE0);
    public static Color Lime => FromArgb(0xFF00FF00);
    public static Color LimeGreen => FromArgb(0xFF32CD32);
    public static Color Linen => FromArgb(0xFFFAF0E6);
    public static Color Magenta => FromArgb(0xFFFF00FF);
    public static Color Maroon => FromArgb(0xFF800000);
    public static Color MediumAquamarine => FromArgb(0xFF66CDAA);
    public static Color MediumBlue => FromArgb(0xFF0000CD);
    public static Color MediumOrchid => FromArgb(0xFFBA55D3);
    public static Color MediumPurple => FromArgb(0xFF9370DB);
    public static Color MediumSeaGreen => FromArgb(0xFF3CB371);
    public static Color MediumSlateBlue => FromArgb(0xFF7B68EE);
    public static Color MediumSpringGreen => FromArgb(0xFF00FA9A);
    public static Color MediumTurquoise => FromArgb(0xFF48D1CC);
    public static Color MediumVioletRed => FromArgb(0xFFC71585);
    public static Color MidnightBlue => FromArgb(0xFF191970);
    public static Color MintCream => FromArgb(0xFFF5FFFA);
    public static Color MistyRose => FromArgb(0xFFFFE4E1);
    public static Color Moccasin => FromArgb(0xFFFFE4B5);
    public static Color NavajoWhite => FromArgb(0xFFFFDEAD);
    public static Color Navy => FromArgb(0xFF000080);
    public static Color OldLace => FromArgb(0xFFFDF5E6);
    public static Color Olive => FromArgb(0xFF808000);
    public static Color OliveDrab => FromArgb(0xFF6B8E23);
    public static Color Orange => FromArgb(0xFFFFA500);
    public static Color OrangeRed => FromArgb(0xFFFF4500);
    public static Color Orchid => FromArgb(0xFFDA70D6);
    public static Color PaleGoldenrod => FromArgb(0xFFEEE8AA);
    public static Color PaleGreen => FromArgb(0xFF98FB98);
    public static Color PaleTurquoise => FromArgb(0xFFAFEEEE);
    public static Color PaleVioletRed => FromArgb(0xFFDB7093);
    public static Color PapayaWhip => FromArgb(0xFFFFEFD5);
    public static Color PeachPuff => FromArgb(0xFFFFDAB9);
    public static Color Peru => FromArgb(0xFFCD853F);
    public static Color Pink => FromArgb(0xFFFFC0CB);
    public static Color Plum => FromArgb(0xFFDDA0DD);
    public static Color PowderBlue => FromArgb(0xFFB0E0E6);
    public static Color Purple => FromArgb(0xFF800080);
    public static Color Red => FromArgb(0xFFFF0000);
    public static Color RosyBrown => FromArgb(0xFFBC8F8F);
    public static Color RoyalBlue => FromArgb(0xFF4169E1);
    public static Color SaddleBrown => FromArgb(0xFF8B4513);
    public static Color Salmon => FromArgb(0xFFFA8072);
    public static Color SandyBrown => FromArgb(0xFFF4A460);
    public static Color SeaGreen => FromArgb(0xFF2E8B57);
    public static Color SeaShell => FromArgb(0xFFFFF5EE);
    public static Color Sienna => FromArgb(0xFFA0522D);
    public static Color Silver => FromArgb(0xFFC0C0C0);
    public static Color SkyBlue => FromArgb(0xFF87CEEB);
    public static Color SlateBlue => FromArgb(0xFF6A5ACD);
    public static Color SlateGray => FromArgb(0xFF708090);
    public static Color Snow => FromArgb(0xFFFFFAFA);
    public static Color SpringGreen => FromArgb(0xFF00FF7F);
    public static Color SteelBlue => FromArgb(0xFF4682B4);
    public static Color Tan => FromArgb(0xFFD2B48C);
    public static Color Teal => FromArgb(0xFF008080);
    public static Color Thistle => FromArgb(0xFFD8BFD8);
    public static Color Tomato => FromArgb(0xFFFF6347);
    public static Color Turquoise => FromArgb(0xFF40E0D0);
    public static Color Violet => FromArgb(0xFFEE82EE);
    public static Color Wheat => FromArgb(0xFFF5DEB3);
    public static Color White => FromArgb(0xFFFFFFFF);
    public static Color WhiteSmoke => FromArgb(0xFFF5F5F5);
    public static Color Yellow => FromArgb(0xFFFFFF00);
    public static Color YellowGreen => FromArgb(0xFF9ACD32);

    // Commonly used color aliases for convenience
    public static Color Clear => Transparent;
    public static Color Opaque => White;

    public static Color RandomGray(Random rng, float min, float range)
    {
        var c = min + (float)rng.NextDouble() * range;
        return new Color(c, c, c, 1.0f);
    }

    public static Color GaussianGray(Random rng, float median, float scale)
    {
        var uniformSum = 0.0;
        for (var index = 0; index < 6; index++)
        {
            uniformSum += rng.NextDouble();
        }

        var standardNormal = (uniformSum - 3.0) * 1.4142135623730951;
        var value = Math.Clamp(median + scale * (float)standardNormal, 0.0f, 1.0f);
        return new Color(value, value, value, 1.0f);
    }
}
