using Nexus.Assets.Typography.FontReader;
using Nexus.Assets.Typography.FontReader.TrueType;

namespace Nexus.Assets.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Decodes simple and composite glyph outlines from a TrueType glyf table.
/// </summary>
public static class GlyfTable
{
    private const byte OnCurvePoint = 0x01;
    private const byte XShortVector = 0x02;
    private const byte YShortVector = 0x04;
    private const byte RepeatFlag = 0x08;
    private const byte XSameOrPositive = 0x10;
    private const byte YSameOrPositive = 0x20;
    private const ushort Arg1And2AreWords = 0x0001;
    private const ushort ArgsAreXyValues = 0x0002;
    private const ushort RoundXyToGrid = 0x0004;
    private const ushort WeHaveAScale = 0x0008;
    private const ushort MoreComponents = 0x0020;
    private const ushort WeHaveAnXAndYScale = 0x0040;
    private const ushort WeHaveATwoByTwo = 0x0080;
    private const ushort WeHaveInstructions = 0x0100;
    private const ushort ScaledComponentOffset = 0x0800;
    private const ushort UnscaledComponentOffset = 0x1000;
    private const ushort KnownComponentFlags = 0x1FEF;
    private const int MaximumCompositeDepth = 32;
    private const int MaximumCompositePointCount = 1_000_000;

    /// <summary>
    /// Parses one glyph from a bounded glyf table reader, resolving composite components recursively.
    /// </summary>
    /// <param name="reader">A reader bounded to the glyf table data.</param>
    /// <param name="locaTable">The validated glyph offsets.</param>
    /// <param name="glyphIndex">The zero-based glyph index.</param>
    /// <returns>The decoded glyph outline.</returns>
    /// <exception cref="ArgumentNullException">A reader or loca table is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="glyphIndex"/> is outside the font.</exception>
    /// <exception cref="InvalidDataException">The glyph data is malformed, cyclic, or excessively deep.</exception>
    public static FontGlyphOutline ParseGlyph(
        TrueTypeReader reader,
        LocaTable locaTable,
        ushort glyphIndex
    )
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(locaTable);
        var range = locaTable.GetGlyphRange(glyphIndex);
        if (range.Length == 0)
            return new FontGlyphOutline([]);

        try
        {
            var contours = ParseGlyph(
                reader,
                locaTable,
                glyphIndex,
                new HashSet<ushort>(),
                depth: 0
            );
            return new FontGlyphOutline(
                contours
                    .Select(contour => new FontContour(
                        contour.Select(point => point.ToFontPoint()).ToArray()
                    ))
                    .ToArray()
            );
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The 'glyf' table contains truncated glyph data.",
                exception
            );
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException(
                "The 'glyf' table contains out-of-range coordinates.",
                exception
            );
        }
    }

    /// <summary>
    /// Resolves a glyph and its components while tracking the active recursion path.
    /// </summary>
    /// <param name="reader">A reader over the complete glyf table.</param>
    /// <param name="locaTable">The validated glyph offsets.</param>
    /// <param name="glyphIndex">The glyph to decode.</param>
    /// <param name="ancestry">Glyphs currently being resolved.</param>
    /// <param name="depth">The number of component levels already entered.</param>
    /// <returns>The decoded glyph outline.</returns>
    /// <exception cref="InvalidDataException">The glyph graph contains a cycle or excessive nesting.</exception>
    private static PrecisePoint[][] ParseGlyph(
        TrueTypeReader reader,
        LocaTable locaTable,
        ushort glyphIndex,
        HashSet<ushort> ancestry,
        int depth
    )
    {
        if (depth >= MaximumCompositeDepth)
            throw new InvalidDataException("A 'glyf' composite exceeds the maximum nesting depth.");
        if (!ancestry.Add(glyphIndex))
            throw new InvalidDataException("A 'glyf' composite contains a component cycle.");

        try
        {
            var range = locaTable.GetGlyphRange(glyphIndex);
            if (range.Length == 0)
                return [];

            var glyphReader = new TrueTypeReader(reader.Slice(range.Offset, range.Length));
            if (glyphReader.Length < 10)
                throw new InvalidDataException("A 'glyf' glyph header is too short.");

            var contourCount = glyphReader.ReadInt16();
            for (var index = 0; index < 4; index++)
                _ = glyphReader.ReadInt16();

            if (contourCount >= 0)
                return ParseSimpleGlyph(glyphReader, contourCount);
            if (contourCount != -1)
                throw new InvalidDataException("A 'glyf' glyph has an invalid contour count.");

            return ParseCompositeGlyph(glyphReader, reader, locaTable, ancestry, depth);
        }
        finally
        {
            ancestry.Remove(glyphIndex);
        }
    }

    /// <summary>
    /// Decodes a glyph header, point flags, and compressed coordinate deltas.
    /// </summary>
    /// <param name="reader">A reader bounded to one glyph's data.</param>
    /// <returns>The decoded glyph outline.</returns>
    /// <exception cref="InvalidDataException">The simple glyph structure is malformed.</exception>
    private static PrecisePoint[][] ParseSimpleGlyph(TrueTypeReader reader, int contourCount)
    {
        if (contourCount == 0)
            return [];

        var endPoints = new ushort[contourCount];
        for (var index = 0; index < endPoints.Length; index++)
        {
            endPoints[index] = reader.ReadUInt16();
            if (index > 0 && endPoints[index] <= endPoints[index - 1])
                throw new InvalidDataException("A 'glyf' glyph has invalid contour endpoints.");
        }

        var pointCount = endPoints[^1] + 1;
        var instructionLength = reader.ReadUInt16();
        for (var index = 0; index < instructionLength; index++)
            _ = reader.ReadUInt8();

        var flags = ReadFlags(reader, pointCount);
        var points = new PrecisePoint[pointCount];
        var xCoordinates = new int[pointCount];
        var yCoordinates = new int[pointCount];
        var x = 0;
        for (var index = 0; index < points.Length; index++)
        {
            x = checked(
                x + ReadCoordinateDelta(reader, flags[index], XShortVector, XSameOrPositive)
            );
            xCoordinates[index] = x;
        }

        var y = 0;
        for (var index = 0; index < points.Length; index++)
        {
            y = checked(
                y + ReadCoordinateDelta(reader, flags[index], YShortVector, YSameOrPositive)
            );
            yCoordinates[index] = y;
            points[index] = new PrecisePoint(
                xCoordinates[index],
                y,
                (flags[index] & OnCurvePoint) != 0
            );
        }

        var contours = new PrecisePoint[endPoints.Length][];
        var firstPoint = 0;
        for (var contourIndex = 0; contourIndex < contours.Length; contourIndex++)
        {
            var lastPoint = endPoints[contourIndex];
            var contourPoints = new PrecisePoint[lastPoint - firstPoint + 1];
            Array.Copy(points, firstPoint, contourPoints, 0, contourPoints.Length);
            contours[contourIndex] = contourPoints;
            firstPoint = lastPoint + 1;
        }

        return contours;
    }

    /// <summary>
    /// Decodes component records, recursively resolves their outlines, and combines transformed contours.
    /// </summary>
    /// <param name="glyphReader">The reader positioned after the composite glyph header.</param>
    /// <param name="glyfReader">A reader over the complete glyf table.</param>
    /// <param name="locaTable">The validated glyph offsets.</param>
    /// <param name="ancestry">Glyphs currently being resolved.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <returns>The assembled composite outline.</returns>
    /// <exception cref="InvalidDataException">A component record or attachment is malformed.</exception>
    private static PrecisePoint[][] ParseCompositeGlyph(
        TrueTypeReader glyphReader,
        TrueTypeReader glyfReader,
        LocaTable locaTable,
        HashSet<ushort> ancestry,
        int depth
    )
    {
        var contours = new List<PrecisePoint[]>();
        var points = new List<PrecisePoint>();
        var hasInstructions = false;
        ushort flags;
        do
        {
            flags = glyphReader.ReadUInt16();
            if ((flags & ~KnownComponentFlags) != 0)
                throw new InvalidDataException("A 'glyf' component contains reserved flag bits.");

            var componentIndex = glyphReader.ReadUInt16();
            if (componentIndex >= locaTable.GlyphCount)
                throw new InvalidDataException(
                    "A 'glyf' composite references an invalid glyph index."
                );

            var argument1 = ReadComponentArgument(glyphReader, flags);
            var argument2 = ReadComponentArgument(glyphReader, flags);
            var (a, b, c, d) = ReadComponentTransform(glyphReader, flags);
            var component = ParseGlyph(glyfReader, locaTable, componentIndex, ancestry, depth + 1);
            var componentPoints = component.SelectMany(contour => contour).ToArray();

            double offsetX;
            double offsetY;
            if ((flags & ArgsAreXyValues) != 0)
            {
                var x = (double)argument1;
                var y = (double)argument2;
                if ((flags & ScaledComponentOffset) != 0)
                {
                    (x, y) = (a * x + c * y, b * x + d * y);
                }

                if ((flags & RoundXyToGrid) != 0)
                {
                    x = Math.Round(x, MidpointRounding.AwayFromZero);
                    y = Math.Round(y, MidpointRounding.AwayFromZero);
                }

                offsetX = x;
                offsetY = y;
            }
            else
            {
                if (argument1 >= points.Count || argument2 >= componentPoints.Length)
                    throw new InvalidDataException(
                        "A 'glyf' composite has an invalid point attachment."
                    );

                var parentPoint = points[argument1];
                var childPoint = TransformPoint(componentPoints[argument2], a, b, c, d, 0, 0);
                offsetX = checked(parentPoint.X - childPoint.X);
                offsetY = checked(parentPoint.Y - childPoint.Y);
            }

            foreach (var contour in component)
            {
                var transformedPoints = contour
                    .Select(point => TransformPoint(point, a, b, c, d, offsetX, offsetY))
                    .ToArray();
                if (transformedPoints.Length > MaximumCompositePointCount - points.Count)
                    throw new InvalidDataException("A 'glyf' composite contains too many points.");

                contours.Add(transformedPoints);
                points.AddRange(transformedPoints);
            }

            hasInstructions |= (flags & WeHaveInstructions) != 0;
        } while ((flags & MoreComponents) != 0);

        if (hasInstructions)
        {
            var instructionLength = glyphReader.ReadUInt16();
            for (var index = 0; index < instructionLength; index++)
                _ = glyphReader.ReadUInt8();
        }

        return contours.ToArray();
    }

    /// <summary>
    /// Reads one signed XY or unsigned point-index component argument.
    /// </summary>
    /// <param name="reader">The component glyph reader.</param>
    /// <param name="flags">The component flags.</param>
    /// <returns>The decoded argument.</returns>
    private static int ReadComponentArgument(TrueTypeReader reader, ushort flags)
    {
        var words = (flags & Arg1And2AreWords) != 0;
        if ((flags & ArgsAreXyValues) != 0)
            return words ? reader.ReadInt16() : unchecked((sbyte)reader.ReadUInt8());

        return words ? reader.ReadUInt16() : reader.ReadUInt8();
    }

    /// <summary>
    /// Reads and validates the optional F2Dot14 component transform.
    /// </summary>
    /// <param name="reader">The component glyph reader.</param>
    /// <param name="flags">The component flags.</param>
    /// <returns>The transform matrix in TrueType coordinate order.</returns>
    private static (double A, double B, double C, double D) ReadComponentTransform(
        TrueTypeReader reader,
        ushort flags
    )
    {
        var transformFlags = flags & (WeHaveAScale | WeHaveAnXAndYScale | WeHaveATwoByTwo);
        if (transformFlags != 0 && (transformFlags & (transformFlags - 1)) != 0)
            throw new InvalidDataException("A 'glyf' component has conflicting transform flags.");

        if (
            (flags & (ScaledComponentOffset | UnscaledComponentOffset))
            == (ScaledComponentOffset | UnscaledComponentOffset)
        )
            throw new InvalidDataException("A 'glyf' component has conflicting offset flags.");

        if (transformFlags == WeHaveAScale)
        {
            var scale = reader.ReadInt16() / 16384.0;
            return (scale, 0, 0, scale);
        }

        if (transformFlags == WeHaveAnXAndYScale)
        {
            var xScale = reader.ReadInt16() / 16384.0;
            var yScale = reader.ReadInt16() / 16384.0;
            return (xScale, 0, 0, yScale);
        }

        if (transformFlags == WeHaveATwoByTwo)
        {
            var a = reader.ReadInt16() / 16384.0;
            var b = reader.ReadInt16() / 16384.0;
            var c = reader.ReadInt16() / 16384.0;
            var d = reader.ReadInt16() / 16384.0;
            return (a, b, c, d);
        }

        return (1, 0, 0, 1);
    }

    /// <summary>
    /// Applies a matrix and component translation to a TrueType point.
    /// </summary>
    /// <param name="point">The source point.</param>
    /// <param name="a">The horizontal-to-horizontal matrix value.</param>
    /// <param name="b">The horizontal-to-vertical matrix value.</param>
    /// <param name="c">The vertical-to-horizontal matrix value.</param>
    /// <param name="d">The vertical-to-vertical matrix value.</param>
    /// <param name="offsetX">The horizontal translation.</param>
    /// <param name="offsetY">The vertical translation.</param>
    /// <returns>The transformed point with fractional precision preserved.</returns>
    private static PrecisePoint TransformPoint(
        PrecisePoint point,
        double a,
        double b,
        double c,
        double d,
        double offsetX,
        double offsetY
    ) =>
        new(
            a * point.X + c * point.Y + offsetX,
            b * point.X + d * point.Y + offsetY,
            point.OnCurve
        );

    /// <summary>
    /// Represents an outline point before its final conversion to integer font units.
    /// </summary>
    /// <param name="X">The horizontal coordinate with fractional precision.</param>
    /// <param name="Y">The vertical coordinate with fractional precision.</param>
    /// <param name="OnCurve">Whether the point lies on the outline curve.</param>
    private readonly record struct PrecisePoint(double X, double Y, bool OnCurve)
    {
        /// <summary>
        /// Converts the point to integer font coordinates using the outline's rounding policy.
        /// </summary>
        /// <returns>The rounded public glyph point.</returns>
        public FontPoint ToFontPoint() =>
            new(
                checked((int)Math.Round(X, MidpointRounding.AwayFromZero)),
                checked((int)Math.Round(Y, MidpointRounding.AwayFromZero)),
                OnCurve
            );
    }

    /// <summary>
    /// Expands repeated point flags to one flag per glyph point.
    /// </summary>
    /// <param name="reader">The glyph reader positioned at the flags.</param>
    /// <param name="pointCount">The number of points in the glyph.</param>
    /// <returns>The expanded point flags.</returns>
    /// <exception cref="InvalidDataException">A repeat run extends beyond the point count.</exception>
    private static byte[] ReadFlags(TrueTypeReader reader, int pointCount)
    {
        var flags = new byte[pointCount];
        var pointIndex = 0;
        while (pointIndex < flags.Length)
        {
            var flag = reader.ReadUInt8();
            flags[pointIndex++] = flag;
            if ((flag & RepeatFlag) == 0)
                continue;

            var repeatCount = reader.ReadUInt8();
            if (repeatCount > flags.Length - pointIndex)
                throw new InvalidDataException(
                    "A 'glyf' flag repeat exceeds the glyph point count."
                );

            Array.Fill(flags, flag, pointIndex, repeatCount);
            pointIndex += repeatCount;
        }

        return flags;
    }

    /// <summary>
    /// Reads one signed coordinate delta according to its TrueType point flag.
    /// </summary>
    /// <param name="reader">The glyph reader positioned at the coordinate data.</param>
    /// <param name="flag">The flags for the point being decoded.</param>
    /// <param name="shortVectorFlag">The flag indicating a one-byte magnitude.</param>
    /// <param name="sameOrPositiveFlag">The flag indicating zero or a positive short delta.</param>
    /// <returns>The signed coordinate delta.</returns>
    private static int ReadCoordinateDelta(
        TrueTypeReader reader,
        byte flag,
        byte shortVectorFlag,
        byte sameOrPositiveFlag
    )
    {
        if ((flag & shortVectorFlag) != 0)
        {
            var magnitude = reader.ReadUInt8();
            return (flag & sameOrPositiveFlag) != 0 ? magnitude : -magnitude;
        }

        return (flag & sameOrPositiveFlag) != 0 ? 0 : reader.ReadInt16();
    }
}
