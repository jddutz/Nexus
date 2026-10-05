using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Nexus.Runtime;

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
    var error = exception.ToString();
    Console.Error.WriteLine(error);
    Debug.WriteLine(error);
    return 1;
}
