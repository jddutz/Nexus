namespace Nexus.AssetPipeline;

using System.Text.Json;
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
                    ProcessFont(asset, sourceRoot, fontEntries);
                }
            }

            WriteManifest(textureEntries, fontEntries);
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
    private void ProcessFont(
        AssetDefinition asset,
        string sourceRoot,
        Dictionary<string, string> fontEntries
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
        PipelineLog.Info($"Font '{asset.ContentId}' copied. RelativePath='{manifestPath}'.");
    }

    private void WriteManifest(
        Dictionary<string, Dictionary<string, string>> textureEntries,
        Dictionary<string, string> fontEntries
    )
    {
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
                ["Content"] = fontEntries.ToDictionary(
                    entry => entry.Key,
                    entry => (object)new Dictionary<string, string> { ["FilePath"] = entry.Value },
                    StringComparer.Ordinal
                ),
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
