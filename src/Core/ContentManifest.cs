namespace Nexus.Core;

public sealed class ContentManifest(string contentLibraryPath, IConfiguration configuration)
    : IContentManifest
{
    public string ContentLibraryPath { get; } = contentLibraryPath;
    public IConfiguration Textures { get; } = configuration.GetSection("Textures");
    public IConfiguration Geometry { get; } = configuration.GetSection("Geometry");
    public IConfiguration Audio { get; } = configuration.GetSection("Audio");
    public IConfiguration Fonts { get; } = configuration.GetSection("Fonts");
}

public static class ContentManagementExtensions
{
    public static T Get<T>(
        this IConfiguration configuration,
        ContentId contentId,
        Func<ContentId, IConfiguration, T> factory
    )
    {
        var section = configuration.GetSection(contentId.Value);

        if (!section.Exists())
        {
            throw new KeyNotFoundException($"Content '{contentId}' was not found.");
        }

        return factory(contentId, section);
    }

    public static string GetRequiredValue(this IConfiguration configuration, string key)
    {
        var section = configuration.GetRequiredSection(key);

        return section.Value
            ?? throw new InvalidOperationException(
                $"Required configuration value '{key}' is null."
            );
    }

    public static string GetContentFilePath(this IConfiguration configuration, ContentId id)
    {
        var filePath = configuration
            .GetRequiredSection("Content")
            .GetRequiredSection(id.Value)
            .GetRequiredValue("FilePath");

        return filePath;
    }
}
