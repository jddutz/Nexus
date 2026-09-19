namespace Nexus.Core;

public sealed class ContentManifest : IContentManifest
{
    public string ContentLibraryPath { get; }
    public IConfiguration Textures { get; }
    public IConfiguration Geometry { get; }
    public IConfiguration Audio { get; }
    public IConfiguration Fonts { get; }

    public ContentManifest(IConfiguration configuration)
    {
        ContentLibraryPath = configuration.GetSection("Path").Value ?? "Content/";
        Textures = configuration.GetSection("Textures");
        Geometry = configuration.GetSection("Geometry");
        Audio = configuration.GetSection("Audio");
        Fonts = configuration.GetSection("Fonts");
    }
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
        var path = configuration.GetRequiredValue("Path");

        var filePath = configuration
            .GetRequiredSection("Content")
            .GetRequiredSection(id.Value)
            .GetRequiredValue("FilePath");

        return Path.Combine(path, filePath);
    }
}
