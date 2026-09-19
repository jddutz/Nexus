namespace Nexus.Core;

public interface IContentManifest
{
    string ContentLibraryPath { get; }
    IConfiguration Textures { get; }
    IConfiguration Geometry { get; }
    IConfiguration Audio { get; }
    IConfiguration Fonts { get; }
}
