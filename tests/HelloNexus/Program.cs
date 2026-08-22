using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.GameEngine.Runtime;

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
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            var services = new ServiceCollection();

            // Register game-specific services here.
            // services.AddMyGameServices();

            var runtime = new RuntimeBuilder(services, configuration).Build();

            var application = new Application(runtime);

            application.Run();

            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Program Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }
    }
}
