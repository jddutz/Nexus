#nullable enable

using System.Text.RegularExpressions;
using System.Xml.Linq;

if (Args.Count < 1 || Args.Count > 2)
{
    Console.Error.WriteLine(
        "Usage: dotnet script scripts/Analyze-TestCoverage.csx -- <coverage-cobertura-xml> [minimum-line-coverage]"
    );
    return;
}

var reportPath = Path.GetFullPath(Args[0]);
if (!File.Exists(reportPath))
    throw new FileNotFoundException("The Cobertura coverage report was not found.", reportPath);

var minimumLineCoverage = Args.Count == 2 ? ParseMinimumLineCoverage(Args[1]) : 0.70;

var report = XDocument.Load(reportPath);
var coverage =
    report.Root ?? throw new InvalidDataException("The coverage report has no root element.");
var classes = coverage.Descendants("class").Select(CreateCoverageClass).ToArray();
var excluded = classes.Where(coverageClass => coverageClass.ExclusionReason is not null).ToArray();
var applicable = classes.Where(coverageClass => coverageClass.ExclusionReason is null).ToArray();
var belowThreshold = applicable
    .Where(coverageClass => coverageClass.LineRate < minimumLineCoverage)
    .OrderBy(coverageClass => coverageClass.LineRate)
    .ThenBy(coverageClass => coverageClass.Name, StringComparer.Ordinal)
    .ToArray();
var policyStatus = GetPolicyStatus(applicable.Length, belowThreshold.Length);

Console.WriteLine($"Report: {reportPath}");
Console.WriteLine($"Overall line coverage: {ParseRate(coverage.Attribute("line-rate")?.Value):P2}");
Console.WriteLine(
    $"Overall branch coverage: {ParseRate(coverage.Attribute("branch-rate")?.Value):P2}"
);
Console.WriteLine();
Console.WriteLine("Coverage policy summary:");
Console.WriteLine($"  Instrumented class entries: {classes.Length}");
Console.WriteLine($"  Excluded entries: {excluded.Length}");
Console.WriteLine($"  Concrete production classes requiring assessment: {applicable.Length}");
Console.WriteLine($"  Classes below {minimumLineCoverage:P0}: {belowThreshold.Length}");
Console.WriteLine($"  Policy status: {policyStatus}");
Console.WriteLine(
    "  Note: Ordinary concrete classes require manual review before they can be treated as data-only."
);

if (belowThreshold.Length > 0)
{
    Console.WriteLine();
    Console.WriteLine("Classes below threshold:");
    foreach (var coverageClass in belowThreshold)
    {
        Console.WriteLine(
            $"  {coverageClass.LineRate, 6:P2} | {coverageClass.Name} | {GetDisplayPath(coverageClass.FileName)}"
        );
    }
}

Console.WriteLine();
Console.WriteLine("Excluded entries:");
foreach (
    var group in excluded
        .GroupBy(coverageClass => coverageClass.ExclusionReason!)
        .OrderBy(group => group.Key)
)
    Console.WriteLine($"  {group.Key}: {group.Count()}");

var excludedDataTypes = excluded
    .Where(coverageClass =>
        coverageClass.ExclusionReason
            is "interface"
                or "record"
                or "struct"
                or "enum"
                or "abstract class"
    )
    .OrderBy(coverageClass => coverageClass.Name, StringComparer.Ordinal)
    .ToArray();

if (excludedDataTypes.Length > 0)
{
    Console.WriteLine();
    Console.WriteLine("Excluded interfaces, records, structs, enums, and abstract classes:");
    foreach (var coverageClass in excludedDataTypes)
        Console.WriteLine($"  {coverageClass.Name} ({coverageClass.ExclusionReason})");
}

/// <summary>
/// Creates a coverage entry from a Cobertura class element.
/// </summary>
/// <param name="classElement">The Cobertura class element.</param>
/// <returns>The parsed coverage entry.</returns>
CoverageClass CreateCoverageClass(XElement classElement)
{
    var name =
        classElement.Attribute("name")?.Value
        ?? throw new InvalidDataException("A Cobertura class entry has no name.");
    var fileName =
        classElement.Attribute("filename")?.Value
        ?? throw new InvalidDataException($"The Cobertura class '{name}' has no filename.");

    return new CoverageClass(
        name,
        fileName,
        ParseRate(classElement.Attribute("line-rate")?.Value),
        GetExclusionReason(name, fileName)
    );
}

/// <summary>
/// Parses a coverage rate value from the Cobertura report.
/// </summary>
/// <param name="value">The serialized coverage rate.</param>
/// <returns>The parsed coverage rate.</returns>
double ParseRate(string? value)
{
    if (
        !double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var rate
        )
    )
        throw new InvalidDataException($"'{value}' is not a valid coverage rate.");

    return rate;
}

/// <summary>
/// Parses the optional minimum line-coverage argument.
/// </summary>
/// <param name="value">The serialized minimum coverage rate.</param>
/// <returns>The minimum coverage rate.</returns>
double ParseMinimumLineCoverage(string value)
{
    var rate = ParseRate(value);
    if (rate is < 0 or > 1)
        throw new ArgumentOutOfRangeException(
            nameof(value),
            "The minimum line coverage must be between 0 and 1."
        );

    return rate;
}

/// <summary>
/// Determines the coverage-policy status for the applicable production classes.
/// </summary>
/// <param name="applicableClassCount">The number of concrete production classes requiring assessment.</param>
/// <param name="belowThresholdCount">The number of applicable classes below the required coverage threshold.</param>
/// <returns>A readable coverage-policy status.</returns>
string GetPolicyStatus(int applicableClassCount, int belowThresholdCount)
{
    if (applicableClassCount == 0)
        return "NOT EVALUATED (no concrete production classes found)";

    return belowThresholdCount == 0 ? "MET" : "NOT MET";
}

/// <summary>
/// Determines whether a coverage entry does not represent an applicable concrete production class.
/// </summary>
/// <param name="className">The coverage entry's class name.</param>
/// <param name="fileName">The source filename recorded by Cobertura.</param>
/// <returns>The exclusion reason, or <see langword="null"/> when the class is applicable.</returns>
string? GetExclusionReason(string className, string fileName)
{
    if (IsTestFile(fileName))
        return "test";

    if (IsCompilerGenerated(className))
        return "compiler generated";

    if (!File.Exists(fileName))
        return null;

    var source = File.ReadAllText(fileName);
    var typeName = className.Split(['.', '+']).Last();
    var escapedTypeName = Regex.Escape(typeName);

    if (Regex.IsMatch(source, $@"\binterface\s+{escapedTypeName}\b"))
        return "interface";
    if (Regex.IsMatch(source, $@"\brecord\s+(?:struct\s+)?{escapedTypeName}\b"))
        return "record";
    if (Regex.IsMatch(source, $@"\benum\s+{escapedTypeName}\b"))
        return "enum";
    if (Regex.IsMatch(source, $@"\bstruct\s+{escapedTypeName}\b"))
        return "struct";
    if (Regex.IsMatch(source, $@"\babstract\s+class\s+{escapedTypeName}\b"))
        return "abstract class";

    return null;
}

/// <summary>
/// Determines whether a filename belongs to a test project.
/// </summary>
/// <param name="fileName">The source filename recorded by Cobertura.</param>
/// <returns><see langword="true"/> when the file is under a tests directory; otherwise, <see langword="false"/>.</returns>
bool IsTestFile(string fileName) =>
    fileName
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(pathSegment =>
            string.Equals(pathSegment, "tests", StringComparison.OrdinalIgnoreCase)
        );

/// <summary>
/// Determines whether a coverage entry is generated by the compiler.
/// </summary>
/// <param name="className">The coverage entry's class name.</param>
/// <returns><see langword="true"/> when the class name identifies a compiler-generated type; otherwise, <see langword="false"/>.</returns>
bool IsCompilerGenerated(string className) =>
    className.Contains('<', StringComparison.Ordinal)
    || className.Contains('>', StringComparison.Ordinal)
    || className.Contains("d__", StringComparison.Ordinal)
    || className.Contains("DisplayClass", StringComparison.Ordinal);

/// <summary>
/// Converts an absolute path inside the working directory to a relative display path.
/// </summary>
/// <param name="fileName">The source filename recorded by Cobertura.</param>
/// <returns>A readable source path.</returns>
string GetDisplayPath(string fileName)
{
    if (!Path.IsPathFullyQualified(fileName))
        return fileName;

    var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), fileName);
    return relativePath.StartsWith("..", StringComparison.Ordinal) ? fileName : relativePath;
}

/// <summary>
/// Represents the coverage result for one Cobertura class entry.
/// </summary>
/// <param name="Name">The fully qualified class name.</param>
/// <param name="FileName">The source filename recorded by Cobertura.</param>
/// <param name="LineRate">The class line coverage rate.</param>
/// <param name="ExclusionReason">The reason this entry is excluded, or <see langword="null"/> when applicable.</param>
record CoverageClass(string Name, string FileName, double LineRate, string? ExclusionReason);
