#r "nuget: System.Drawing.Common, 10.0.0"

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

if (Args.Count < 1 || Args.Count > 2)
{
    Console.Error.WriteLine("Usage: dotnet script Create-TextureAtlas.csx -- <input-directory> [output-directory]");
    return;
}

var inputDirectory = Path.GetFullPath(Args[0]);
var outputDirectory = Path.GetFullPath(Args.Count == 2 ? Args[1] : Path.Combine(inputDirectory, "Atlas"));

if (!Directory.Exists(inputDirectory))
{
    throw new DirectoryNotFoundException($"Input directory '{inputDirectory}' was not found.");
}

var files = Directory
    .EnumerateFiles(inputDirectory, "*.png", SearchOption.TopDirectoryOnly)
    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (files.Length == 0)
{
    throw new InvalidOperationException($"No PNG files were found in '{inputDirectory}'.");
}

var tileWidth = 128;
var tileHeight = 128;
var columns = (int)Math.Ceiling(Math.Sqrt(files.Length));
var rows = (int)Math.Ceiling(files.Length / (double)columns);
var atlasWidth = columns * tileWidth;
var atlasHeight = rows * tileHeight;
var regions = new Dictionary<string, AtlasRegion>(StringComparer.OrdinalIgnoreCase);

Directory.CreateDirectory(outputDirectory);
var atlasPath = Path.Combine(outputDirectory, "gui-atlas.png");
var mapPath = Path.Combine(outputDirectory, "gui-atlas.json");

using var atlas = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppArgb);
using (var graphics = Graphics.FromImage(atlas))
{
    graphics.CompositingMode = CompositingMode.SourceCopy;
    graphics.Clear(Color.Transparent);
    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
    graphics.PixelOffsetMode = PixelOffsetMode.Half;

    for (var index = 0; index < files.Length; index++)
    {
        var path = files[index];
        using var source = new Bitmap(path);

        if (source.Width != tileWidth || source.Height != tileHeight)
        {
            throw new InvalidDataException(
                $"'{Path.GetFileName(path)}' is {source.Width}x{source.Height}; expected {tileWidth}x{tileHeight}.");
        }

        var x = (index % columns) * tileWidth;
        var y = (index / columns) * tileHeight;
        graphics.DrawImage(source, new Rectangle(x, y, tileWidth, tileHeight));

        var name = Path.GetFileNameWithoutExtension(path);
        regions.Add(name, new AtlasRegion(
            x,
            y,
            tileWidth,
            tileHeight,
            x / (float)atlasWidth,
            y / (float)atlasHeight,
            tileWidth / (float)atlasWidth,
            tileHeight / (float)atlasHeight));
    }
}

atlas.Save(atlasPath, ImageFormat.Png);

var metadata = new AtlasMetadata(atlasWidth, atlasHeight, tileWidth, tileHeight, columns, rows, regions);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(mapPath, JsonSerializer.Serialize(metadata, jsonOptions));

Console.WriteLine($"Created {atlasPath}");
Console.WriteLine($"Created {mapPath}");
Console.WriteLine($"{files.Length} images, {columns} columns x {rows} rows, {atlasWidth}x{atlasHeight} pixels");

record AtlasMetadata(
    int Width,
    int Height,
    int TileWidth,
    int TileHeight,
    int Columns,
    int Rows,
    Dictionary<string, AtlasRegion> Regions);

record AtlasRegion(
    int X,
    int Y,
    int Width,
    int Height,
    float U,
    float V,
    float UScale,
    float VScale);