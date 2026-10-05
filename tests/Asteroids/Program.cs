using System.Diagnostics;
using Microsoft.Extensions.Configuration;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
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
