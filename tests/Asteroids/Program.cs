try
{
    var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{env}.json", optional: true)
        .AddCommandLine(args)
        .Build();

    using var application = new Application(configuration);
    application.Run();

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Debug.WriteLine(exception);
    return 1;
}
