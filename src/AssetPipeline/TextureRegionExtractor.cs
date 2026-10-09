using Silk.NET.Maths;

namespace Nexus.AssetPipeline;

/// <summary>Optional alpha-mask extraction settings, applied to each imported image.</summary>
public sealed class TextureRegionSettings
{
    public bool Enabled { get; set; } = true;
    public int AlphaThreshold { get; set; } = 16;
    public int MinimumIslandArea { get; set; } = 64;
    public int MergeDistance { get; set; }
    public int Padding { get; set; } = 2;
    public int RowTolerance { get; set; } = 2;
    public Dictionary<string, int[]> Groups { get; set; } = [];
    public Dictionary<string, RegionBounds> NamedBounds { get; set; } = [];
}

public sealed class RegionBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>Detects eight-connected alpha islands in top-left pixel coordinates.</summary>
public static class TextureRegionExtractor
{
    public static IReadOnlyList<Nexus.Graphics.Textures.TextureRegion> Extract(
        byte[] rgba, int width, int height, TextureRegionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(settings);
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Expected positive dimensions and packed RGBA pixels.");
        if (settings.AlphaThreshold is < 0 or > 255 || settings.MinimumIslandArea < 1
            || settings.MergeDistance < 0 || settings.Padding < 0 || settings.RowTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(settings));
        var seen = new bool[width * height];
        var islands = new List<(int X, int Y, int R, int B)>();
        var queue = new Queue<int>();
        for (var start = 0; start < seen.Length; start++)
        {
            if (seen[start] || rgba[start * 4 + 3] <= settings.AlphaThreshold) continue;
            seen[start] = true;
            queue.Enqueue(start);
            int left = width, top = height, right = 0, bottom = 0, area = 0;
            while (queue.TryDequeue(out var pixel))
            {
                int x = pixel % width, y = pixel / width;
                left = Math.Min(left, x); top = Math.Min(top, y);
                right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1); area++;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    var next = ny * width + nx;
                    if (seen[next] || rgba[next * 4 + 3] <= settings.AlphaThreshold) continue;
                    seen[next] = true; queue.Enqueue(next);
                }
            }
            if (area >= settings.MinimumIslandArea) islands.Add((left, top, right, bottom));
        }
        // Merge original island bounds transitively; padding never affects grouping.
        var parents = Enumerable.Range(0, islands.Count).ToArray();
        int Root(int i) { while (parents[i] != i) i = parents[i]; return i; }
        if (settings.MergeDistance > 0)
            for (var i = 0; i < islands.Count; i++)
            for (var j = i + 1; j < islands.Count; j++)
            {
                var a = islands[i]; var b = islands[j];
                var gapX = Math.Max(0, Math.Max((long)a.X - b.R, (long)b.X - a.R));
                var gapY = Math.Max(0, Math.Max((long)a.Y - b.B, (long)b.Y - a.B));
                if (Math.Max(gapX, gapY) <= settings.MergeDistance) parents[Root(j)] = Root(i);
            }
        var candidates = islands.Select((bounds, i) => (bounds, root: Root(i)))
            .GroupBy(item => item.root).Select(group => (
                X: group.Min(item => item.bounds.X), Y: group.Min(item => item.bounds.Y),
                R: group.Max(item => item.bounds.R), B: group.Max(item => item.bounds.B)))
            .OrderBy(bounds => bounds.Y).ThenBy(bounds => bounds.X).ToArray();
        // Anchor each row to its first top coordinate, avoiding transitive drift
        // and non-transitive comparisons when artwork is slightly misaligned.
        for (var start = 0; start < candidates.Length;)
        {
            var end = start + 1;
            while (end < candidates.Length && (long)candidates[end].Y - candidates[start].Y <= settings.RowTolerance)
                end++;
            Array.Sort(candidates, start, end - start,
                Comparer<(int X, int Y, int R, int B)>.Create((a, b) =>
                {
                    var column = a.X.CompareTo(b.X);
                    return column != 0 ? column : a.Y.CompareTo(b.Y);
                }));
            start = end;
        }
        var result = new Dictionary<string, Nexus.Graphics.Textures.TextureRegion>(StringComparer.Ordinal);
        var used = new HashSet<int>();
        void Add(string name, int x, int y, int r, int b, bool pad)
        {
            if (string.IsNullOrWhiteSpace(name) || x < 0 || y < 0 || r <= x || b <= y || r > width || b > height)
                throw new ArgumentException("Region names and bounds must be valid and inside the image.");
            var p = pad ? settings.Padding : 0;
            x = (int)Math.Max(0, (long)x - p); y = (int)Math.Max(0, (long)y - p);
            r = (int)Math.Min(width, (long)r + p); b = (int)Math.Min(height, (long)b + p);
            result.Add(name, new(name, new Rectangle<int>(x, y, r - x, b - y),
                new Rectangle<float>((float)x / width, (float)y / height, (float)(r - x) / width, (float)(b - y) / height)));
        }
        foreach (var (name, indices) in settings.Groups)
        {
            if (indices.Length == 0 || indices.Any(i => i < 0 || i >= candidates.Length || !used.Add(i)))
                throw new ArgumentException("Groups require nonempty, unique candidate indices.");
            Add(name, indices.Min(i => candidates[i].X), indices.Min(i => candidates[i].Y),
                indices.Max(i => candidates[i].R), indices.Max(i => candidates[i].B), true);
        }
        for (var i = 0; i < candidates.Length; i++)
            if (!used.Contains(i))
            {
                var c = candidates[i]; Add($"region-{i:D4}", c.X, c.Y, c.R, c.B, true);
            }
        foreach (var (name, b) in settings.NamedBounds)
        {
            result.Remove(name);
            Add(name, b.X, b.Y, checked(b.X + b.Width), checked(b.Y + b.Height), false);
        }
        return result.Values.ToArray();
    }
}
