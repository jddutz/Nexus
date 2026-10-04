namespace Nexus.Graphics.Text;

using Nexus.Assets.Fonts;

/// <summary>
/// Builds and caches text styles and their generated font atlas textures.
/// </summary>
/// <param name="manifest">Provides the configured content root and font file mappings.</param>
/// <param name="fontBuilder">Builds generated font atlas data.</param>
public sealed class TextStyleRegistry(
    IContentManifest manifest,
    IFontBuilder fontBuilder
) : ITextStyleRegistry
{
    private readonly IContentManifest _manifest =
        manifest ?? throw new ArgumentNullException(nameof(manifest));
    private readonly IFontBuilder _fontBuilder =
        fontBuilder ?? throw new ArgumentNullException(nameof(fontBuilder));
    private readonly Dictionary<TextStyleDescription, TextStyleId> _descriptions = [];
    private readonly Dictionary<TextStyleId, ITextStyle> _styles = [];
    private readonly Dictionary<ContentId, ITexture> _atlasTextures = [];
    private readonly Dictionary<RasterKey, CachedRaster> _rasters = [];

    /// <inheritdoc />
    public ITextStyle GetOrCreate(TextStyleDescription description)
    {
        ValidateDescription(description);
        if (_descriptions.TryGetValue(description, out var styleId))
            return _styles[styleId];

        var raster = GetOrCreateRaster(description);
        var texture = CreateAtlasTexture(raster);
        var style = new TextStyle(raster.Font, texture, description.Size);
        _descriptions.Add(description, style.Id);
        _styles.TryAdd(style.Id, style);
        return _styles[style.Id];
    }

    /// <inheritdoc />
    public ITextStyle Get(TextStyleId textstyle)
    {
        if (!_styles.TryGetValue(textstyle, out var style))
            throw new KeyNotFoundException($"Text style '{textstyle}' is not registered.");

        return style;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _descriptions.Clear();
        _styles.Clear();
        _atlasTextures.Clear();
        _rasters.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Reset();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets a compatible raster or builds one when the requested size is outside every
    /// cached raster's reusable range.
    /// </summary>
    /// <param name="description">The requested text style description.</param>
    /// <returns>The selected or newly generated raster.</returns>
    private CachedRaster GetOrCreateRaster(TextStyleDescription description)
    {
        var compatible = _rasters.Values
            .Where(raster =>
                raster.FontResourceId == description.FontResourceId
                && description.Size >= raster.EmSize * 0.5f
                && description.Size <= raster.EmSize * 2f
            )
            .OrderBy(raster => Math.Abs(raster.EmSize - description.Size))
            .FirstOrDefault();
        if (compatible is not null)
            return compatible;

        var emSize = Math.Clamp((int)MathF.Round(description.Size), 16, 256);
        var key = new RasterKey(description.FontResourceId, emSize);
        if (_rasters.TryGetValue(key, out var cached))
            return cached;

        var raster = new CachedRaster(
            description.FontResourceId,
            emSize,
            BuildFont(description.FontResourceId, emSize)
        );
        _rasters.Add(key, raster);
        return raster;
    }

    /// <summary>Builds the default glyph repertoire at the requested raster size.</summary>
    /// <param name="fontResourceId">The font content identifier.</param>
    /// <param name="emSize">The raster generation size in pixels per em.</param>
    /// <returns>The generated font data.</returns>
    private FontBuildResult BuildFont(ContentId fontResourceId, int emSize)
    {
        var filepath = Path.Combine(
            _manifest.ContentLibraryPath,
            _manifest.Fonts.GetContentFilePath(fontResourceId)
        );
        return _fontBuilder.Build(
            filepath,
            new FontGlyphRepertoire().GetCodepoints(),
            new FontGenerationSettings { EmSize = emSize }
        );
    }

    /// <summary>Creates and registers a texture from generated RGB atlas data.</summary>
    /// <param name="raster">The generated font raster.</param>
    /// <returns>The registered atlas texture.</returns>
    private ITexture CreateAtlasTexture(CachedRaster raster)
    {
        var atlas = raster.Font.Atlas;
        var fontResourceId = raster.FontResourceId;
        var contentId = (ContentId)$"{fontResourceId}:text-atlas:{raster.EmSize}";
        if (_atlasTextures.TryGetValue(contentId, out var cached))
            return cached;

        if (atlas.Width <= 0 || atlas.Height <= 0)
            throw new InvalidOperationException("The generated font atlas dimensions are invalid.");

        var expectedLength = checked(atlas.Width * atlas.Height * 3);
        if (atlas.Pixels.Length != expectedLength)
            throw new InvalidOperationException("The generated font atlas data is invalid.");

        var colors = new Color[checked(atlas.Width * atlas.Height)];
        for (var index = 0; index < colors.Length; index++)
        {
            var offset = index * 3;
            colors[index] = new Color(
                atlas.Pixels[offset],
                atlas.Pixels[offset + 1],
                atlas.Pixels[offset + 2]
            );
        }

        var texture = new Texture(
            contentId,
            (uint)atlas.Width,
            (uint)atlas.Height,
            colors,
            ColorFormatEnum.RGB8UNorm
        );
        _atlasTextures.Add(contentId, texture);
        return texture;
    }

    /// <summary>Stores one generated raster that can be reused across compatible sizes.</summary>
    /// <param name="FontResourceId">The source font content identifier.</param>
    /// <param name="EmSize">The generation size of the raster.</param>
    /// <param name="Font">The generated font data.</param>
    private sealed record CachedRaster(
        ContentId FontResourceId,
        int EmSize,
        FontBuildResult Font
    );

    /// <summary>Identifies a generated raster by source font and resolved generation size.</summary>
    /// <param name="FontResourceId">The source font content identifier.</param>
    /// <param name="EmSize">The resolved raster generation size.</param>
    private sealed record RasterKey(ContentId FontResourceId, int EmSize);

    /// <summary>Validates the public text style description.</summary>
    /// <param name="description">The description to validate.</param>
    private static void ValidateDescription(TextStyleDescription description)
    {
        if (string.IsNullOrWhiteSpace(description.FontFamily))
            throw new ArgumentException("Font family is required.", nameof(description));
        if (description.FontResourceId == ContentId.Invalid)
            throw new ArgumentException("Font resource ID is required.", nameof(description));
        if (!float.IsFinite(description.Size) || description.Size <= 0f)
            throw new ArgumentOutOfRangeException(nameof(description), "Size must be positive and finite.");
        // TODO: Apply WidthFactor during text layout before accepting non-default values.
        if (!float.IsFinite(description.WidthFactor) || description.WidthFactor <= 0f)
            throw new ArgumentOutOfRangeException(nameof(description), "Width factor must be positive and finite.");
        // TODO: Apply OutlineWidth during font generation and text rendering before accepting non-default values.
        if (!float.IsFinite(description.OutlineWidth) || description.OutlineWidth < 0f)
            throw new ArgumentOutOfRangeException(nameof(description), "Outline width must be finite and non-negative.");
    }
}
