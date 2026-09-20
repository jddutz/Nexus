using System.Runtime.InteropServices;

namespace Nexus.AssetPipeline.Fonts;

/// <summary>Managed owner for the narrow Nexus font-generation C ABI.</summary>
public sealed class NativeFontRasterizer : IFontRasterizer
{
    public FontBuildResult Rasterize(
        string sourcePath,
        IReadOnlyList<int> codepoints,
        FontGenerationSettings settings
    )
    {
        var requestedCodepoints = codepoints.ToArray();
        PipelineLog.Info(
            $"NativeFontRasterizer.Rasterize called: SourcePath='{sourcePath}', CodepointCount={requestedCodepoints.Length}, "
                + $"EmSize={settings.EmSize}, DistanceRange={settings.DistanceRange}, Padding={settings.Padding}."
        );
        IntPtr resultPointer = IntPtr.Zero;
        IntPtr errorPointer = IntPtr.Zero;
        try
        {
            var status = NativeMethods.Generate(
                sourcePath,
                requestedCodepoints,
                (nuint)requestedCodepoints.Length,
                settings.EmSize,
                settings.DistanceRange,
                settings.Padding,
                out resultPointer,
                out errorPointer
            );
            PipelineLog.Info(
                $"NativeMethods.Generate returned Status={status}, ResultPointer=0x{resultPointer.ToInt64():X}, "
                    + $"ErrorPointer=0x{errorPointer.ToInt64():X}."
            );
            if (status != 0 || resultPointer == IntPtr.Zero)
            {
                var message =
                    errorPointer == IntPtr.Zero
                        ? $"Native rasterizer failed with status {status}."
                        : Marshal.PtrToStringUTF8(errorPointer)!;
                throw new FontBuildException(message);
            }

            var managedResult = CopyToManaged(Marshal.PtrToStructure<NativeResult>(resultPointer));
            PipelineLog.Info(
                $"NativeFontRasterizer.Rasterize returned atlas {managedResult.Atlas.Width}x{managedResult.Atlas.Height}, "
                    + $"Pixels={managedResult.Atlas.Pixels.Length}, Glyphs={managedResult.Glyphs.Count}, Kerning={managedResult.Kerning.Count}."
            );
            return managedResult;
        }
        catch (DllNotFoundException exception)
        {
            throw new FontBuildException(
                "The NAP native font rasterizer could not be loaded. Ensure the binary for the current runtime identifier is deployed beside nap.",
                exception
            );
        }
        catch (EntryPointNotFoundException exception)
        {
            throw new FontBuildException(
                "The NAP native font rasterizer has an incompatible ABI.",
                exception
            );
        }
        finally
        {
            if (resultPointer != IntPtr.Zero)
                NativeMethods.FreeResult(resultPointer);
            if (errorPointer != IntPtr.Zero)
                NativeMethods.FreeError(errorPointer);
        }
    }

    private static FontBuildResult CopyToManaged(NativeResult native)
    {
        PipelineLog.Info(
            $"NativeFontRasterizer.CopyToManaged called: NativeAtlas={native.Width}x{native.Height}, "
                + $"GlyphCount={native.GlyphCount}, KerningCount={native.KerningCount}."
        );
        var pixelCount = checked(native.Width * native.Height * 3);
        var pixels = new byte[pixelCount];
        Marshal.Copy(native.Pixels, pixels, 0, pixelCount);

        var glyphs = CopyArray<NativeGlyph>(native.Glyphs, native.GlyphCount)
            .Select(glyph => new FontGlyph(
                glyph.Codepoint,
                glyph.Advance,
                glyph.PlaneBounds.ToManaged(),
                glyph.AtlasBounds.ToManaged()
            ))
            .ToArray();
        var kerning = CopyArray<NativeKerning>(native.Kerning, native.KerningCount)
            .Select(pair => new FontKerningPair(pair.Left, pair.Right, pair.Adjustment))
            .ToArray();

        var result = new FontBuildResult(
            new FontAtlas(native.Width, native.Height, pixels),
            new FontMetrics(native.EmSize, native.Ascender, native.Descender, native.LineHeight),
            glyphs,
            kerning,
            new MsdfMetadata(native.DistanceRange, native.GenerationEmSize)
        );
        PipelineLog.Info(
            $"NativeFontRasterizer.CopyToManaged returned: Pixels={result.Atlas.Pixels.Length}, "
                + $"Glyphs={result.Glyphs.Count}, Kerning={result.Kerning.Count}."
        );
        return result;
    }

    private static T[] CopyArray<T>(IntPtr source, int count)
        where T : struct
    {
        if (count == 0)
            return [];
        var result = new T[count];
        var size = Marshal.SizeOf<T>();
        for (var index = 0; index < count; index++)
            result[index] = Marshal.PtrToStructure<T>(IntPtr.Add(source, checked(index * size)));
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBounds
    {
        public double Left;
        public double Bottom;
        public double Right;
        public double Top;

        public readonly FontBounds ToManaged() => new(Left, Bottom, Right, Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeGlyph
    {
        public int Codepoint;
        public double Advance;
        public NativeBounds PlaneBounds;
        public NativeBounds AtlasBounds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKerning
    {
        public int Left;
        public int Right;
        public double Adjustment;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeResult
    {
        public int Width;
        public int Height;
        public IntPtr Pixels;
        public double EmSize;
        public double Ascender;
        public double Descender;
        public double LineHeight;
        public IntPtr Glyphs;
        public int GlyphCount;
        public IntPtr Kerning;
        public int KerningCount;
        public double DistanceRange;
        public int GenerationEmSize;
    }

    private static class NativeMethods
    {
        private const string LibraryName = "nexus_font_native";

        [DllImport(
            LibraryName,
            EntryPoint = "nap_font_generate",
            CallingConvention = CallingConvention.Cdecl
        )]
        internal static extern int Generate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            int[] codepoints,
            nuint codepointCount,
            int emSize,
            double distanceRange,
            int padding,
            out IntPtr result,
            out IntPtr error
        );

        [DllImport(
            LibraryName,
            EntryPoint = "nap_font_result_free",
            CallingConvention = CallingConvention.Cdecl
        )]
        internal static extern void FreeResult(IntPtr result);

        [DllImport(
            LibraryName,
            EntryPoint = "nap_font_error_free",
            CallingConvention = CallingConvention.Cdecl
        )]
        internal static extern void FreeError(IntPtr error);
    }
}
