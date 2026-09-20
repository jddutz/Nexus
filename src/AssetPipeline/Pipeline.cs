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
            var processor = new FontProcessor(new NativeFontRasterizer());
            var textureEntries = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.Ordinal
            );
            PipelineLog.Info("YAML deserializer and font processor created.");

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
                        $"Asset[{assetIndex}] parsed: Type='{asset.AssetType}', ContentId='{asset.ContentId}', Source='{asset.Source}', Path='{asset.Path}', Group='{asset.GroupName}', Files=[{string.Join(", ", asset.Files)}], HasGlyphs={asset.Glyphs is not null}, HasGeneration={asset.Generation is not null}."
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
                    var font = FontDefinition.FromAsset(asset);
                    PipelineLog.Info(
                        $"Asset[{assetIndex}] converted to FontDefinition: ContentId='{font.ContentId}', Source='{font.Source}', Repertoire='{font.Glyphs.Repertoire}', Characters='{font.Glyphs.Characters}', EmSize={font.Generation.EmSize}, DistanceRange={font.Generation.DistanceRange}, Padding={font.Generation.Padding}."
                    );
                    var result = processor.Process(font, sourceRoot);
                    PipelineLog.Info(
                        $"Font processor returned atlas {result.Atlas.Width}x{result.Atlas.Height}, Pixels={result.Atlas.Pixels.Length}, Glyphs={result.Glyphs.Count}, Kerning={result.Kerning.Count}."
                    );
                    var outputPath = FontProcessor.GetOutputPath(_outputFolder, font.ContentId);
                    PipelineLog.Info($"Writing font package to '{outputPath}'.");
                    FontPackageWriter.Write(outputPath, result);
                    PipelineLog.Info(
                        $"Font package write returned successfully for '{font.ContentId}'."
                    );
                }
            }

            WriteManifest(textureEntries);
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

    private void WriteManifest(Dictionary<string, Dictionary<string, string>> textureEntries)
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
                ["Content"] = new Dictionary<string, string>(),
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
