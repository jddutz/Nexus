var inputOption = new Option<string[]>("--input", "-i")
{
    Description = "Asset pipeline definition file(s).",
    Required = true,
    AllowMultipleArgumentsPerToken = true,
};

PipelineLog.Info(
    $"Starting nap. Args=[{string.Join(", ", args.Select(argument => "'" + argument + "'"))}]"
);

var outputOption = new Option<string>("--output", "-o")
{
    Description = "Root output folder for generated content.",
    Required = true,
};

var buildCommand = new Command("build", "Build game assets into runtime content") { inputOption };

buildCommand.SetAction(parseResult =>
{
    var inputFiles = parseResult.GetValue(inputOption);
    var outputPath = parseResult.GetValue(outputOption);
    PipelineLog.Info(
        $"Parsed build options: Input=[{string.Join(", ", inputFiles ?? [])}], Output='{outputPath}'"
    );

    var result = new Pipeline(inputFiles ?? [], outputPath!).Execute();
    PipelineLog.Info($"Build action returned {result}.");
    return result;
});

var cleanCommand = new Command("clean", "Clean generated Nexus Asset Pipeline content") { };

cleanCommand.SetAction(parseResult =>
{
    var outputPath = parseResult.GetValue(outputOption);
    PipelineLog.Info($"Parsed clean options: Output='{outputPath}'");

    if (string.IsNullOrWhiteSpace(outputPath))
    {
        PipelineLog.Error("Clean did not receive a usable output path.");
        return 1;
    }

    var fullOutputPath = Path.GetFullPath(outputPath);
    var existed = Directory.Exists(fullOutputPath);
    PipelineLog.Info($"Clean resolved output path '{fullOutputPath}'. Exists={existed}.");

    if (existed)
        Directory.Delete(fullOutputPath, recursive: true);

    PipelineLog.Info($"Clean completed. Deleted={existed}. ReturnCode=0.");
    return 0;
});

var rootCommand = new RootCommand("Nexus Asset Pipeline") { outputOption };

rootCommand.Subcommands.Add(buildCommand);
rootCommand.Subcommands.Add(cleanCommand);

var parseResult = rootCommand.Parse(args);
PipelineLog.Info($"Command parse completed. Errors={parseResult.Errors.Count}.");
foreach (var error in parseResult.Errors)
    PipelineLog.Error($"Command parse error: {error.Message}");

var exitCode = parseResult.Invoke();
PipelineLog.Info($"Command invocation returned {exitCode}.");
return exitCode;
