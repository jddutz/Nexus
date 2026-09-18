var inputOption = new Option<string[]>("--input", "-i")
{
    Description = "Asset pipeline definition file(s).",
    Required = true,
    AllowMultipleArgumentsPerToken = true,
};

var outputOption = new Option<string>("--output", "-o")
{
    Description = "Root output folder for generated content.",
    Required = true,
};

var buildCommand = new Command("build", "Build game assets into runtime content")
{
    inputOption,
    outputOption,
};

buildCommand.SetAction(parseResult =>
{
    return new Pipeline(
        parseResult.GetValue(inputOption)!,
        parseResult.GetValue(outputOption)!
    ).Execute();
});

var rootCommand = new RootCommand("Nexus Asset Pipeline") { buildCommand };

return rootCommand.Parse(args).Invoke();
