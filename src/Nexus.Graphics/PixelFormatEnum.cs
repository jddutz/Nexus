namespace Nexus.Graphics;

public enum ColorFormatEnum
{
    RGB8UNorm,
    RGBA8UNorm,
    ARGB8UNorm,

    RGB16UNorm,
    RGBA16UNorm,
    ARGB16UNorm,

    RGB16Float,
    RGBA16Float,

    RGB32UInt,
    RGBA32UInt,

    RGB32Float,
    RGBA32Float,
}

public static class ColorFormatExtensions
{
    public static int GetBytesPerPixel(this ColorFormatEnum format) =>
        format switch
        {
            ColorFormatEnum.RGB8UNorm => 3,
            ColorFormatEnum.RGBA8UNorm or ColorFormatEnum.ARGB8UNorm => 4,
            ColorFormatEnum.RGB16UNorm or ColorFormatEnum.RGB16Float => 6,
            ColorFormatEnum.RGBA16UNorm
            or ColorFormatEnum.ARGB16UNorm
            or ColorFormatEnum.RGBA16Float => 8,
            ColorFormatEnum.RGB32UInt or ColorFormatEnum.RGB32Float => 12,
            ColorFormatEnum.RGBA32UInt or ColorFormatEnum.RGBA32Float => 16,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
}
