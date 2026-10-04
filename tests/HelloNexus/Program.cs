try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .AddJsonFile(".content/content-manifest.json")
        .AddCommandLine(args)
        .Build();

    using var application = new Application(configuration);
    application.Run();

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
