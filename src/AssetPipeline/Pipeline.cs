namespace Nexus.AssetPipeline;

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
        // Build pipeline
        return 0;
    }
}
