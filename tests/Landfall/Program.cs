using Microsoft.Extensions.Configuration;
using Nexus.Runtime;

try
{
    var configurationBuilder = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json", optional: true)
        .AddCommandLine(args);
    var startupConfiguration = configurationBuilder.Build();
    var manifestLocation = startupConfiguration["Application:ContentManifestLocation"]
        ?? ".content/content-manifest.json";
    var manifestPath = Path.IsPathRooted(manifestLocation)
        ? manifestLocation
        : Path.Combine(AppContext.BaseDirectory, manifestLocation);
    var configuration = configurationBuilder
        .AddJsonFile(manifestPath)
        .AddCommandLine(args)
        .Build();
    using var application = new Application(configuration);
    application.Run();
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
