using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HelloNexus;

/// <summary>
/// Entry point for the Hello Nexus application.
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        Environment.ExitCode = -1;

        try
        {
            Console.WriteLine("Starting Hello Nexus...");

            var configuration = new ConfigurationBuilder()
            //.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
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
