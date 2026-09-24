using System.Runtime.InteropServices;
using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Fonts;

/// <summary>Managed owner for the narrow Nexus font-generation C ABI.</summary>
public sealed class NativeFontRasterizer : IFontRasterizer
{
    /// <summary>
    /// Generates glyph imagery natively and attaches kerning parsed by the managed TrueType reader.
    /// </summary>
    /// <param name="sourcePath">The source TrueType or OpenType font path.</param>
    /// <param name="codepoints">The Unicode codepoints required by the font build.</param>
    /// <param name="settings">The atlas and distance-field generation settings.</param>
    /// <returns>The atlas, glyph metadata, font metrics, and codepoint kerning pairs.</returns>
    /// <exception cref="FontBuildException">The native rasterizer cannot generate the font result.</exception>
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
            var kerning = TrueTypeFontReader.Open(sourcePath).GetKerningPairs(requestedCodepoints);
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

            var managedResult = CopyToManaged(
                Marshal.PtrToStructure<NativeResult>(resultPointer),
                kerning
            );
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

    /// <summary>
    /// Copies native atlas and glyph data into managed values with reader-owned kerning.
    /// </summary>
    /// <param name="native">The native result structure.</param>
    /// <param name="kerning">The managed codepoint kerning pairs.</param>
    /// <returns>The fully managed font build result.</returns>
    private static FontBuildResult CopyToManaged(
        NativeResult native,
        IReadOnlyList<TextKerningPair> kerning
    )
    {
        PipelineLog.Info(
            $"NativeFontRasterizer.CopyToManaged called: NativeAtlas={native.Width}x{native.Height}, "
                + $"GlyphCount={native.GlyphCount}, KerningCount={kerning.Count}."
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

    /// <summary>
    /// Copies a contiguous native array into managed values.
    /// </summary>
    /// <typeparam name="T">The blittable native structure type.</typeparam>
    /// <param name="source">The first native array element.</param>
    /// <param name="count">The number of elements to copy.</param>
    /// <returns>The managed array.</returns>
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

        /// <summary>
        /// Converts the native bounds to the managed font-bounds representation.
        /// </summary>
        /// <returns>The managed bounds.</returns>
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

        /// <summary>
        /// Generates atlas data and glyph metrics using the native font library.
        /// </summary>
        /// <param name="path">The UTF-8 font path.</param>
        /// <param name="codepoints">The requested Unicode codepoints.</param>
        /// <param name="codepointCount">The number of requested codepoints.</param>
        /// <param name="emSize">The generation em size in pixels.</param>
        /// <param name="distanceRange">The MSDF distance range.</param>
        /// <param name="padding">The atlas padding in pixels.</param>
        /// <param name="result">Receives the native result pointer.</param>
        /// <param name="error">Receives a native UTF-8 error pointer on failure.</param>
        /// <returns>Zero on success; otherwise a native error status.</returns>
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

        /// <summary>
        /// Releases the native result and all arrays it owns.
        /// </summary>
        /// <param name="result">The native result pointer.</param>
        [DllImport(
            LibraryName,
            EntryPoint = "nap_font_result_free",
            CallingConvention = CallingConvention.Cdecl
        )]
        internal static extern void FreeResult(IntPtr result);

        /// <summary>
        /// Releases an error string returned by the native generator.
        /// </summary>
        /// <param name="error">The native error pointer.</param>
        [DllImport(
            LibraryName,
            EntryPoint = "nap_font_error_free",
            CallingConvention = CallingConvention.Cdecl
        )]
        internal static extern void FreeError(IntPtr error);
    }
}
