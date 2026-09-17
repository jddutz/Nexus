namespace Nexus.Studio;

/// <summary>
/// Entry point for the Nexus Studio application.
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        Environment.ExitCode = -1;

        try
        {
            Console.WriteLine("Starting Nexus Studio...");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
                .Build();

            var services = new ServiceCollection();

            // Register game-specific services here.
            // services.AddMyGameServices();

            using var application = new Application(configuration, services);

            application.Run();

            Environment.ExitCode = 0;
            Console.WriteLine("Nexus Studio exited normally.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Nexus Studio encountered an unhandled error:");
            Console.Error.WriteLine(ex);
        }
    }
}
