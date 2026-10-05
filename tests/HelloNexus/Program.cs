using System.Diagnostics;

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
    var error = ex.ToString();
    Console.Error.WriteLine(error);
    Debug.WriteLine(error);
    return 1;
}
