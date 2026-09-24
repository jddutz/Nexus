using Nexus.AssetPipeline.Typography.FontReader;
using Nexus.AssetPipeline.Typography.FontReader.TrueType;

namespace Nexus.AssetPipeline.Typography.FontReader.TrueType.Tables;

/// <summary>
/// Decodes simple glyph outlines from a TrueType glyf table.
/// </summary>
public static class GlyfTable
{
    private const byte OnCurvePoint = 0x01;
    private const byte XShortVector = 0x02;
    private const byte YShortVector = 0x04;
    private const byte RepeatFlag = 0x08;
    private const byte XSameOrPositive = 0x10;
    private const byte YSameOrPositive = 0x20;

    /// <summary>
    /// Parses one simple glyph from a bounded glyf table reader.
    /// </summary>
    /// <param name="reader">A reader bounded to the glyf table data.</param>
    /// <param name="locaTable">The validated glyph offsets.</param>
    /// <param name="glyphIndex">The zero-based glyph index.</param>
    /// <returns>The decoded glyph outline.</returns>
    /// <exception cref="ArgumentNullException">A reader or loca table is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="glyphIndex"/> is outside the font.</exception>
    /// <exception cref="InvalidDataException">The glyph data is malformed.</exception>
    /// <exception cref="NotSupportedException">The glyph is composite rather than simple.</exception>
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
            return ParseSimpleGlyph(new TrueTypeReader(reader.Slice(range.Offset, range.Length)));
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
    /// Decodes a glyph header, point flags, and compressed coordinate deltas.
    /// </summary>
    /// <param name="reader">A reader bounded to one glyph's data.</param>
    /// <returns>The decoded glyph outline.</returns>
    /// <exception cref="InvalidDataException">The simple glyph structure is malformed.</exception>
    /// <exception cref="NotSupportedException">The glyph is composite rather than simple.</exception>
    private static FontGlyphOutline ParseSimpleGlyph(TrueTypeReader reader)
    {
        if (reader.Length < 10)
            throw new InvalidDataException("A 'glyf' glyph header is too short.");

        var contourCount = reader.ReadInt16();
        for (var index = 0; index < 4; index++)
            _ = reader.ReadInt16();

        if (contourCount < 0)
        {
            if (contourCount == -1)
                throw new NotSupportedException("Composite TrueType glyphs are not supported yet.");

            throw new InvalidDataException("A 'glyf' glyph has an invalid contour count.");
        }

        if (contourCount == 0)
            return new FontGlyphOutline([]);

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
        var points = new FontPoint[pointCount];
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
            points[index] = new FontPoint(
                xCoordinates[index],
                y,
                (flags[index] & OnCurvePoint) != 0
            );
        }

        var contours = new FontContour[endPoints.Length];
        var firstPoint = 0;
        for (var contourIndex = 0; contourIndex < contours.Length; contourIndex++)
        {
            var lastPoint = endPoints[contourIndex];
            var contourPoints = new FontPoint[lastPoint - firstPoint + 1];
            Array.Copy(points, firstPoint, contourPoints, 0, contourPoints.Length);
            contours[contourIndex] = new FontContour(contourPoints);
            firstPoint = lastPoint + 1;
        }

        return new FontGlyphOutline(contours);
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
