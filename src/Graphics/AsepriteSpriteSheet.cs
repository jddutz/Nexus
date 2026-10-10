using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Nexus.Graphics;

/// <summary>Animations from an untrimmed Aseprite atlas, grouped by tags or exported layer names.</summary>
public sealed class AsepriteSpriteSheet
{
    private readonly Dictionary<string, SpriteFrame[]> _animations = new(StringComparer.OrdinalIgnoreCase);
    public int AtlasWidth { get; private set; }
    public int AtlasHeight { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }

    public SpriteAnimation GetAnimation(string name, bool loop = false) => new(_animations[name], loop);

    public static AsepriteSpriteSheet Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var meta = root.GetProperty("meta");
        var size = meta.GetProperty("size");
        var sheet = new AsepriteSpriteSheet
        {
            AtlasWidth = size.GetProperty("w").GetInt32(),
            AtlasHeight = size.GetProperty("h").GetInt32(),
        };
        if (sheet.AtlasWidth <= 0 || sheet.AtlasHeight <= 0)
            throw new InvalidDataException("Aseprite atlas dimensions must be positive.");
        var source = root.GetProperty("frames");
        var entries = source.ValueKind == JsonValueKind.Array
            ? source.EnumerateArray().Select(frame => (Name: frame.GetProperty("filename").GetString()!, Frame: frame)).ToArray()
            : source.EnumerateObject().Select(frame => (Name: frame.Name, Frame: frame.Value)).ToArray();
        var frames = new List<SpriteFrame>();
        foreach (var (_, frame) in entries)
        {
            if (frame.GetProperty("rotated").GetBoolean() || frame.GetProperty("trimmed").GetBoolean())
                throw new InvalidDataException("Export Aseprite frames without trimming or rotation.");
            var rect = frame.GetProperty("frame");
            int Read(string key) => rect.GetProperty(key).GetInt32();
            var (x, y, w, h) = (Read("x"), Read("y"), Read("w"), Read("h"));
            if (frames.Count == 0) { sheet.FrameWidth = w; sheet.FrameHeight = h; }
            if (w <= 0 || h <= 0 || w != sheet.FrameWidth || h != sheet.FrameHeight
                || x < 0 || y < 0 || (long)x + w > sheet.AtlasWidth || (long)y + h > sheet.AtlasHeight)
                throw new InvalidDataException("Aseprite frames must have a shared size and lie within the atlas.");
            var duration = frame.GetProperty("duration").GetInt32();
            if (duration <= 0) throw new InvalidDataException("Aseprite frame durations must be positive.");
            frames.Add(new(new((float)x / sheet.AtlasWidth, (float)y / sheet.AtlasHeight,
                (float)w / sheet.AtlasWidth, (float)h / sheet.AtlasHeight), TimeSpan.FromMilliseconds(duration)));
        }
        if (frames.Count == 0) throw new InvalidDataException("Aseprite export contains no frames.");
        foreach (var tag in meta.GetProperty("frameTags").EnumerateArray())
        {
            var from = tag.GetProperty("from").GetInt32();
            var to = tag.GetProperty("to").GetInt32();
            if (from < 0 || to < from || to >= frames.Count) throw new InvalidDataException("Invalid Aseprite tag range.");
            var indices = Enumerable.Range(from, to - from + 1).ToArray();
            var direction = tag.GetProperty("direction").GetString();
            if (direction is "reverse" or "pingpong_reverse") Array.Reverse(indices);
            if (direction is "pingpong" or "pingpong_reverse")
                indices = indices.Concat(indices.Skip(1).SkipLast(1).Reverse()).ToArray();
            sheet._animations.Add(tag.GetProperty("name").GetString()!, indices.Select(index => frames[index]).ToArray());
        }
        // Layer exports use "character (Layer) 0.aseprite" and may contain no tags.
        var layers = entries.Select((entry, index) => (Match: Regex.Match(entry.Name, @"\((?<name>[^)]+)\) (?<index>\d+)\."), Index: index))
            .Where(entry => entry.Match.Success).GroupBy(entry => entry.Match.Groups["name"].Value);
        foreach (var layer in layers)
            sheet._animations.TryAdd(layer.Key, layer.OrderBy(entry => int.Parse(entry.Match.Groups["index"].Value,
                System.Globalization.CultureInfo.InvariantCulture)).Select(entry => frames[entry.Index]).ToArray());
        return sheet;
    }
}

public static class SpriteAnimationManifestExtensions
{
    /// <summary>Loads the texture's companion animation export, or null for standalone artwork.</summary>
    public static AsepriteSpriteSheet? GetSpriteSheet(this IContentManifest manifest, ContentId textureId)
    {
        var path = manifest.Textures.GetRequiredSection("Content").GetRequiredSection(textureId.Value)["AnimationsFilePath"];
        return path is null ? null : AsepriteSpriteSheet.Load(Path.Combine(manifest.ContentLibraryPath, path));
    }
}
