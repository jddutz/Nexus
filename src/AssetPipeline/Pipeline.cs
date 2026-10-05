namespace Nexus.AssetPipeline;

using System.Text.Json;
using Nexus.Assets.Typography.FontReader.OpenType;
using Nexus.Assets.Typography.Geometry;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class Pipeline
{
    private readonly string[] _inputFiles;
    private readonly string _outputFolder;

    public Pipeline(string[] inputFiles, string outputFolder)
    {
        _inputFiles = inputFiles;
        _outputFolder = outputFolder;
        PipelineLog.Info(
            $"Pipeline created. Inputs=[{string.Join(", ", inputFiles)}], Output='{outputFolder}'."
        );
    }

    public int Execute()
    {
        PipelineLog.Info($"Pipeline execution started. InputCount={_inputFiles.Length}.");
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var textureEntries = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal
            );
            var fontEntries = new Dictionary<string, string>(StringComparer.Ordinal);
                var fontRasterizerInputs = new Dictionary<string, FontRasterizerInput>(
                    StringComparer.Ordinal
                );
                PipelineLog.Info("YAML deserializer created.");

            foreach (var inputFile in _inputFiles.Order(StringComparer.Ordinal))
            {
                var definitionPath = Path.GetFullPath(inputFile);
                PipelineLog.Info(
                    $"Reading definition '{inputFile}' as '{definitionPath}'. Exists={File.Exists(definitionPath)}."
                );
                var yaml = File.ReadAllText(definitionPath);
                PipelineLog.Info(
                    $"Parsed source text for '{definitionPath}':{Environment.NewLine}{yaml}"
                );
                var pipeline = deserializer.Deserialize<PipelineDefinition>(yaml);
                PipelineLog.Info(
                    $"Deserialized '{definitionPath}': Root='{pipeline.Root}', AssetCount={pipeline.Assets.Length}."
                );
                var definitionFolder = Path.GetDirectoryName(definitionPath)!;
                var sourceRoot = Path.GetFullPath(pipeline.Root, definitionFolder);
                PipelineLog.Info(
                    $"Resolved source root '{sourceRoot}'. Exists={Directory.Exists(sourceRoot)}."
                );

                for (var assetIndex = 0; assetIndex < pipeline.Assets.Length; assetIndex++)
                {
                    var asset = pipeline.Assets[assetIndex];
                    PipelineLog.Info(
                        $"Asset[{assetIndex}] parsed: Type='{asset.AssetType}', ContentId='{asset.ContentId}', Source='{asset.Source}', Path='{asset.Path}', Group='{asset.GroupName}', Files=[{string.Join(", ", asset.Files)}]."
                    );
                    if (!string.Equals(asset.AssetType, "font", StringComparison.OrdinalIgnoreCase))
                    {
                        if (
                            string.Equals(
                                asset.AssetType,
                                "texture",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            ProcessTextures(asset, sourceRoot, textureEntries);
                            continue;
                        }

                        PipelineLog.Info(
                            $"Asset[{assetIndex}] skipped because type '{asset.AssetType}' is not supported by the current processor."
                        );
                        continue;
                    }
                    ProcessFont(asset, sourceRoot, fontEntries, fontRasterizerInputs);
                }
            }

            WriteManifest(textureEntries, fontEntries, fontRasterizerInputs);
            PipelineLog.Info("Pipeline execution completed successfully. ReturnCode=0.");
            return 0;
        }
        catch (Exception exception)
        {
            PipelineLog.Error("NAP build failed.", exception);
            PipelineLog.Info("Pipeline execution completed with ReturnCode=1.");
            return 1;
        }
    }

    private void ProcessTextures(
        AssetDefinition asset,
        string sourceRoot,
        Dictionary<string, Dictionary<string, string>> textureEntries
    )
    {
        var textureSection = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceFolder = Path.Combine(sourceRoot, asset.Path);
        var outputFolder = Path.Combine(_outputFolder, asset.Path);
        Directory.CreateDirectory(outputFolder);

        foreach (var file in asset.Files)
        {
            var sourcePath = Path.GetFullPath(Path.Combine(sourceFolder, file));
            var outputPath = Path.GetFullPath(Path.Combine(outputFolder, file));
            var contentId = Path.GetFileNameWithoutExtension(file);

            PipelineLog.Info(
                $"Texture '{contentId}': Source='{sourcePath}', Output='{outputPath}', "
                    + $"SourceExists={File.Exists(sourcePath)}."
            );

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Texture source file was not found.", sourcePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(sourcePath, outputPath, overwrite: true);

            var relativeOutputPath = Path.GetRelativePath(_outputFolder, outputPath)
                .Replace('\\', '/');
            textureSection[contentId] = relativeOutputPath;
            PipelineLog.Info($"Texture '{contentId}' copied. RelativePath='{relativeOutputPath}'.");
        }

        textureEntries[asset.GroupName.Length == 0 ? "Textures" : asset.GroupName] = textureSection;
    }

    /// <summary>
    /// Copies a TrueType or OpenType source file into the content output and records its path.
    /// </summary>
    /// <param name="asset">The font asset definition.</param>
    /// <param name="sourceRoot">The root used to resolve the source font path.</param>
    /// <param name="fontEntries">The manifest entries to update.</param>
    /// <param name="fontRasterizerInputs">The optional rasterizer input data to update.</param>
    private void ProcessFont(
        AssetDefinition asset,
        string sourceRoot,
        Dictionary<string, string> fontEntries,
        Dictionary<string, FontRasterizerInput> fontRasterizerInputs
    )
    {
        if (string.IsNullOrWhiteSpace(asset.ContentId))
            throw new InvalidOperationException("A Font asset must specify contentId.");
        if (
            asset.ContentId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || asset.ContentId.Contains('/')
            || asset.ContentId.Contains('\\')
        )
            throw new InvalidOperationException(
                $"Font '{asset.ContentId}' has an invalid contentId."
            );
        if (string.IsNullOrWhiteSpace(asset.Source))
            throw new InvalidOperationException(
                $"Font '{asset.ContentId}' must specify a source file."
            );

        var sourcePath = Path.GetFullPath(asset.Source, sourceRoot);
        var extension = Path.GetExtension(sourcePath);
        if (
            !string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException(
                $"Font '{asset.ContentId}' source '{sourcePath}' must be a .ttf or .otf file."
            );
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Font source file was not found.", sourcePath);

        var relativeOutputPath = Path.Combine("fonts", asset.ContentId + extension);
        var outputPath = Path.Combine(_outputFolder, relativeOutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.Copy(sourcePath, outputPath, overwrite: true);

        if (asset.IncludeMsdf)
        {
            var definition = new FontDefinition(
                asset.ContentId,
                asset.Source,
                new FontGlyphRepertoire(),
                new FontGenerationSettings()
            );
            var result = new FontProcessor(new FontBuilder()).Process(definition, sourceRoot);
            FontAtlasWriter.WritePng(Path.ChangeExtension(outputPath, ".png"), result);
        }

        var manifestPath = relativeOutputPath.Replace('\\', '/');
        fontEntries[asset.ContentId] = manifestPath;
        if (asset.IncludeRasterizerInput)
            fontRasterizerInputs[asset.ContentId] = ReadRasterizerInput(sourcePath);
        PipelineLog.Info($"Font '{asset.ContentId}' copied. RelativePath='{manifestPath}'.");
    }

    /// <summary>
    /// Reads the font data and converts glyph outlines into the line and quadratic geometry
    /// consumed by the font rasterizer.
    /// </summary>
    /// <param name="sourcePath">The source font path.</param>
    /// <returns>The rasterizer input for the default NAP glyph repertoire.</returns>
    private static FontRasterizerInput ReadRasterizerInput(string sourcePath)
    {
        var reader = OpenTypeFontReader.Open(sourcePath);
        var face = reader.FontFace;
        var codepoints = new FontGlyphRepertoire().GetCodepoints();
        var glyphs = codepoints
            .Select(codepoint =>
            {
                var glyphIndex = reader.GetGlyphIndex(codepoint);
                var metrics = reader.GetHorizontalMetrics(glyphIndex);
                var contours = reader
                    .GetGlyphContours(glyphIndex)
                    .ToArray();
                var bounds = GeometryBoundsCalculator.GetBounds(contours);

                return new FontRasterizerGlyph(
                    codepoint,
                    glyphIndex,
                    metrics.AdvanceWidth,
                    metrics.LeftSideBearing,
                    bounds is { } value
                        ? new FontRasterizerBounds(
                            value.Left,
                            value.Bottom,
                            value.Right,
                            value.Top
                        )
                        : null,
                    contours
                        .Select(contour => new FontRasterizerContour(
                            contour.Edges.Select(ToRasterizerSegment).ToArray()
                        ))
                        .ToArray()
                );
            })
            .ToArray();
        var generationSettings = new FontGenerationSettings();

        return new FontRasterizerInput(
            new FontRasterizerMetrics(
                face.UnitsPerEm,
                face.Ascender,
                face.Descender,
                face.LineGap,
                face.GlyphCount,
                face.NumberOfHorizontalMetrics,
                face.IndexToLocFormat
            ),
            new FontRasterizerGenerationSettings(
                generationSettings.EmSize,
                generationSettings.DistanceRange,
                generationSettings.Padding
            ),
            glyphs,
            reader.GetKerningPairs(codepoints)
        );
    }

    /// <summary>
    /// Converts rasterizer geometry into its serializable segment representation.
    /// </summary>
    /// <param name="edge">The geometry edge to convert.</param>
    /// <returns>The corresponding line or quadratic segment.</returns>
    /// <exception cref="NotSupportedException">The edge type is not consumed by the font rasterizer.</exception>
    private static FontRasterizerSegment ToRasterizerSegment(Edge edge) =>
        edge switch
        {
            LineSegment line => new FontRasterizerSegment(
                "line",
                new FontRasterizerPoint(line.Start.X, line.Start.Y),
                null,
                new FontRasterizerPoint(line.End.X, line.End.Y)
            ),
            QuadraticSegment quadratic => new FontRasterizerSegment(
                "quadratic",
                new FontRasterizerPoint(quadratic.Start.X, quadratic.Start.Y),
                new FontRasterizerPoint(quadratic.Control.X, quadratic.Control.Y),
                new FontRasterizerPoint(quadratic.End.X, quadratic.End.Y)
            ),
            _ => throw new NotSupportedException(
                $"The font rasterizer does not support edge type '{edge.GetType().Name}'."
            ),
        };

    private void WriteManifest(
        Dictionary<string, Dictionary<string, string>> textureEntries,
        Dictionary<string, string> fontEntries,
        Dictionary<string, FontRasterizerInput> fontRasterizerInputs
    )
    {
        var fontContent = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var entry in fontEntries)
        {
            var font = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["FilePath"] = entry.Value,
            };
            if (fontRasterizerInputs.TryGetValue(entry.Key, out var rasterizerInput))
                font["RasterizerInput"] = rasterizerInput;
            fontContent[entry.Key] = font;
        }

        var content = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Textures"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["Content"] = textureEntries
                    .Values.SelectMany(entries => entries)
                    .ToDictionary(
                        entry => entry.Key,
                        entry =>
                            (object)new Dictionary<string, string> { ["FilePath"] = entry.Value },
                        StringComparer.Ordinal
                    ),
            },
            ["Geometry"] = new Dictionary<string, object>
            {
                ["Content"] = new Dictionary<string, string>(),
            },
            ["Audio"] = new Dictionary<string, object>
            {
                ["Content"] = new Dictionary<string, string>(),
            },
            ["Fonts"] = new Dictionary<string, object>
            {
                ["Content"] = fontContent,
            },
        };

        Directory.CreateDirectory(_outputFolder);
        var manifestPath = Path.Combine(_outputFolder, "content-manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true })
        );
        PipelineLog.Info($"Content manifest written to '{manifestPath}'.");
    }
}
