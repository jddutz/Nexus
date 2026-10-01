namespace Nexus.Graphics.Components;

/// <summary>Draws a resizable texture region as four corners, four edges, and a center.</summary>
public partial class NinePatchComponent : TextureComponent
{
    [Observable(PublicSetter = true)]
    private Vector4D<float> _sourceBorders;

    [Observable(PublicSetter = true)]
    private Vector4D<float>? _destinationBorders;

    /// <summary>Initializes a nine-patch component with a corner-pivoted quad by default.</summary>
    /// <param name="centered">Whether each patch quad is centered on its origin.</param>
    public NinePatchComponent(bool centered = false)
        : base(centered) { }

    /// <inheritdoc />
    public override string DisplayName => "Nine Patch";

    private void BeforeSourceBordersChanges(Vector4D<float> value)
    {
        if (
            !float.IsFinite(value.X)
            || !float.IsFinite(value.Y)
            || !float.IsFinite(value.Z)
            || !float.IsFinite(value.W)
            || value.X < 0f
            || value.Y < 0f
            || value.Z < 0f
            || value.W < 0f
        )
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Source borders must be finite and non-negative."
            );

        ValidateSourceBordersValue(value);
    }

    private void BeforeDestinationBordersChanges(Vector4D<float>? value)
    {
        if (value is { } borders)
            ValidateDestinationBordersValue(borders);
    }

    private void AfterSourceBordersChanges(Vector4D<float> previousValue) =>
        NotifyInstanceDataChanged();

    private void AfterDestinationBordersChanges(Vector4D<float>? previousValue) =>
        NotifyInstanceDataChanged();

    /// <inheritdoc />
    protected override int GetInstanceCount() => 9;

    /// <inheritdoc />
    protected override void ValidateTextureInput(Texture? texture, Vector4D<float> texCoord)
    {
        if (
            !float.IsFinite(texCoord.X)
            || !float.IsFinite(texCoord.Y)
            || !float.IsFinite(texCoord.Z)
            || !float.IsFinite(texCoord.W)
            || texCoord.X < 0f
            || texCoord.Y < 0f
            || texCoord.Z <= 0f
            || texCoord.W <= 0f
            || texCoord.X + texCoord.Z > 1f
            || texCoord.Y + texCoord.W > 1f
        )
            throw new ArgumentOutOfRangeException(
                nameof(texCoord),
                "The source region must fit within the texture UV range."
            );

        if (texture is not null)
            ValidateBordersFit(_sourceBorders, texture, texCoord);
    }

    /// <inheritdoc />
    protected override void ValidateSourceBordersValue(Vector4D<float> sourceBorders)
    {
        if (Texture is not null)
            ValidateBordersFit(sourceBorders, Texture, TexCoord);
    }

    /// <inheritdoc />
    protected override void GetInstance(
        int instanceIndex,
        out Matrix4X4<float> transform,
        out Vector4D<float> texCoord
    )
    {
        if ((uint)instanceIndex >= 9u)
            throw new ArgumentOutOfRangeException(nameof(instanceIndex));

        var textureWidth = Texture?.Width ?? 1f;
        var textureHeight = Texture?.Height ?? 1f;
        var sourceLeft = SourceBorders.X / textureWidth;
        var sourceTop = SourceBorders.Y / textureHeight;
        var sourceRight = SourceBorders.Z / textureWidth;
        var sourceBottom = SourceBorders.W / textureHeight;
        var destinationBorders = DestinationBorders ?? SourceBorders;
        var destinationLeft = FitBorders(
            destinationBorders.X,
            destinationBorders.Z,
            Size.X,
            out var destinationRight
        );
        var destinationTop = FitBorders(
            destinationBorders.Y,
            destinationBorders.W,
            Size.Y,
            out var destinationBottom
        );
        var destinationWidths = new[]
        {
            destinationLeft,
            Size.X - destinationLeft - destinationRight,
            destinationRight,
        };
        var destinationHeights = new[]
        {
            destinationTop,
            Size.Y - destinationTop - destinationBottom,
            destinationBottom,
        };
        var sourceWidths = new[] { sourceLeft, TexCoord.Z - sourceLeft - sourceRight, sourceRight };
        var sourceHeights = new[]
        {
            sourceTop,
            TexCoord.W - sourceTop - sourceBottom,
            sourceBottom,
        };
        var column = instanceIndex % 3;
        var row = instanceIndex / 3;
        var x = column == 0 ? 0f : destinationWidths[0] + (column == 2 ? destinationWidths[1] : 0f);
        var y = row == 0 ? 0f : destinationHeights[0] + (row == 2 ? destinationHeights[1] : 0f);
        var u =
            TexCoord.X
            + (column == 0 ? 0f : sourceWidths[0] + (column == 2 ? sourceWidths[1] : 0f));
        var v =
            TexCoord.Y + (row == 0 ? 0f : sourceHeights[0] + (row == 2 ? sourceHeights[1] : 0f));

        transform = CreateRectangleTransform(
            x,
            y,
            destinationWidths[column],
            destinationHeights[row],
            IsCentered
        );
        texCoord = InsetTexCoord(
            new(u, v, sourceWidths[column], sourceHeights[row]),
            insetLeft: column == 0,
            insetTop: row == 0,
            insetRight: column == 2,
            insetBottom: row == 2
        );
    }

    /// <summary>Fits opposing destination borders within a possibly smaller destination.</summary>
    /// <param name="leading">The left or top border.</param>
    /// <param name="trailing">The right or bottom border.</param>
    /// <param name="extent">The destination extent.</param>
    /// <param name="fittedTrailing">The fitted right or bottom border.</param>
    /// <returns>The fitted left or top border.</returns>
    private static float FitBorders(
        float leading,
        float trailing,
        float extent,
        out float fittedTrailing
    )
    {
        var borderExtent = leading + trailing;
        var scale = borderExtent > extent && borderExtent > 0f ? extent / borderExtent : 1f;
        fittedTrailing = trailing * scale;
        return leading * scale;
    }

    /// <summary>Ensures source-pixel border sums fit the selected UV rectangle.</summary>
    /// <param name="sourceBorders">The source border widths.</param>
    /// <param name="texture">The source texture.</param>
    /// <param name="texCoord">The selected UV rectangle.</param>
    private static void ValidateBordersFit(
        Vector4D<float> sourceBorders,
        Texture texture,
        Vector4D<float> texCoord
    )
    {
        if (
            sourceBorders.X + sourceBorders.Z > texCoord.Z * texture.Width
            || sourceBorders.Y + sourceBorders.W > texCoord.W * texture.Height
        )
            throw new ArgumentOutOfRangeException(
                nameof(sourceBorders),
                "Source borders must fit within the selected texture region."
            );
    }

    /// <summary>Ensures destination border widths are finite and non-negative.</summary>
    /// <param name="destinationBorders">The proposed logical border widths.</param>
    private static void ValidateDestinationBordersValue(Vector4D<float> destinationBorders)
    {
        if (
            !float.IsFinite(destinationBorders.X)
            || !float.IsFinite(destinationBorders.Y)
            || !float.IsFinite(destinationBorders.Z)
            || !float.IsFinite(destinationBorders.W)
            || destinationBorders.X < 0f
            || destinationBorders.Y < 0f
            || destinationBorders.Z < 0f
            || destinationBorders.W < 0f
        )
            throw new ArgumentOutOfRangeException(
                nameof(destinationBorders),
                "Destination borders must be finite and non-negative."
            );
    }
}
