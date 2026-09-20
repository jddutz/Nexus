namespace Nexus.AssetPipeline;

internal static class PipelineLog
{
    public static void Info(string message) => Console.WriteLine($"[NAP] {message}");

    public static void Error(string message, Exception? exception = null)
    {
        Console.Error.WriteLine($"[NAP ERROR] {message}");
        if (exception is not null)
        {
            Console.Error.WriteLine(exception);
        }
    }

    public static string Describe(string value) => value.Replace(Environment.NewLine, "\\n");
}
