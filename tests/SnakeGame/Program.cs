using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Nexus.Runtime;

try
{
    var configurationBuilder = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json");

    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    if (!string.IsNullOrWhiteSpace(environment))
        configurationBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true);

    var configuration = configurationBuilder.AddCommandLine(args).Build();

    using var application = new Application(configuration);
    application.Run();

    return 0;
}
catch (Exception exception)
{
    var error = exception.ToString();
    Console.Error.WriteLine(error);
    Debug.WriteLine(error);
    return 1;
}
