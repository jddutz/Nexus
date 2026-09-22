using System.Xml.Linq;

if (Args.Count < 1 || Args.Count > 2)
{
    Console.Error.WriteLine(
        "Usage: dotnet script Extract-VulkanCommandNames.csx -- <commands-xml> [output-file]"
    );
    return;
}

var inputPath = Path.GetFullPath(Args[0]);
if (!File.Exists(inputPath))
    throw new FileNotFoundException("The command XML file was not found.", inputPath);

var names =
    XDocument
        .Load(inputPath)
        .Root?.Elements("command")
        .Select(command => command.Element("proto")?.Element("name")?.Value)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .ToArray()
    ?? [];

if (names.Length == 0)
    throw new InvalidDataException($"No command names were found in '{inputPath}'.");

var output = string.Join(Environment.NewLine, names) + Environment.NewLine;

if (Args.Count == 2)
{
    var outputPath = Path.GetFullPath(Args[1]);
    File.WriteAllText(outputPath, output);
}
else
{
    Console.Write(output);
}
