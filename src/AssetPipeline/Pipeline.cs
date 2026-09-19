namespace Nexus.AssetPipeline;

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
    }

    public int Execute()
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var processor = new FontProcessor(new NativeFontRasterizer());

            foreach (var inputFile in _inputFiles.Order(StringComparer.Ordinal))
            {
                var definitionPath = Path.GetFullPath(inputFile);
                var pipeline = deserializer.Deserialize<PipelineDefinition>(File.ReadAllText(definitionPath));
                var definitionFolder = Path.GetDirectoryName(definitionPath)!;
                var sourceRoot = Path.GetFullPath(pipeline.Root, definitionFolder);

                foreach (var asset in pipeline.Assets)
                {
                    if (!string.Equals(asset.AssetType, "font", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var font = FontDefinition.FromAsset(asset);
                    var result = processor.Process(font, sourceRoot);
                    FontPackageWriter.Write(
                        FontProcessor.GetOutputPath(_outputFolder, font.ContentId),
                        result
                    );
                }
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"NAP build failed: {exception.Message}");
            return 1;
        }
    }
}
