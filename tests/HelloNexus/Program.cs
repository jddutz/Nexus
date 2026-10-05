using System.Diagnostics;

try
{
    var configurationBuilder = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .AddJsonFile(".content/content-manifest.json");

    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    if (!string.IsNullOrWhiteSpace(environment))
        configurationBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true);

    var configuration = configurationBuilder.AddCommandLine(args).Build();

    using var application = new Application(configuration);
    application.Run();

    return 0;
}
catch (Exception ex)
{
    var error = ex.ToString();
    Console.Error.WriteLine(error);
    Debug.WriteLine(error);
    return 1;
}
