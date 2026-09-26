namespace HelloNexus;

/// <summary>
/// Entry point for the Hello Nexus application.
/// </summary>
internal static class Program
{
    /// <summary>Creates the application configuration and runs Hello Nexus.</summary>
    /// <param name="args">Command-line configuration overrides.</param>
    private static void Main(string[] args)
    {
        Environment.ExitCode = -1;

        try
        {
            Console.WriteLine("Starting Hello Nexus...");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
                .AddJsonFile(
                    Path.Combine(AppContext.BaseDirectory, ".content", "content-manifest.json")
                )
                .AddCommandLine(args)
                .Build();

            var services = new ServiceCollection();

            // Register game-specific services here.
            // services.AddMyGameServices();

            using var application = new Application(configuration, services);

            application.Run();

            Environment.ExitCode = 0;
            Console.WriteLine("Hello Nexus exited normally.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Hello Nexus encountered an unhandled error:");
            Console.Error.WriteLine(ex);
        }
    }
}
