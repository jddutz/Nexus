using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Cameras;
using Nexus.Graphics.Components;
using Nexus.Graphics.Text;
using Nexus.Graphics.Textures;
using Nexus.GUI;
using Nexus.GUI.Elements;
using Nexus.Input;
using Nexus.Input.Events;
using Silk.NET.Maths;

namespace Nexus.Samples.Landfall;

/// <summary>The playable Landfall sample, rendered with Nexus tile maps and atlas regions.</summary>
[Scene("Landfall")]
public sealed class LandfallScene : Scene
{
    private readonly LandfallGame _game = new();
    private readonly IWindowService _windows;
    private readonly IEventHub _events;
    private readonly ITextStyleRegistry _styles;
    private readonly ITexture _atlas;
    private readonly StaticCamera _camera = new();
    private readonly TileMapData _board = new(new(0, 0, 20, 20));
    private readonly TileMapData _fog = new(new(0, 0, 20, 20));
    private readonly TileMapData _resources = new(new(0, 0, 20, 20));
    private readonly TileMapData _buildings = new(new(0, 0, 20, 20));
    private readonly TileMapData _preview = new(new(-5, -5, 30, 30));
    private readonly TileMapRenderer _boardRenderer;
    private readonly TileMapRenderer _fogRenderer;
    private readonly TileMapRenderer _resourceRenderer;
    private readonly TileMapRenderer _buildingRenderer;
    private readonly TileMapRenderer _previewRenderer;
    private readonly GameObject2D _boardObject;
    private readonly GameObject2D _fogObject;
    private readonly GameObject2D _resourceObject;
    private readonly GameObject2D _buildingObject;
    private readonly GameObject2D _previewObject;
    private readonly TileMapData[] _trayMaps = new TileMapData[5];
    private readonly TileMapRenderer[] _trayRenderers = new TileMapRenderer[5];
    private readonly GameObject2D[] _trayObjects = new GameObject2D[5];
    private readonly ImageElement[] _cards = new ImageElement[5];
    private readonly TextElement[] _labels = new TextElement[5];
    private readonly ImageElement[] _gridLines = new ImageElement[42];
    private readonly HashSet<KeyEnum> _held = [];
    private readonly TextElement _status;
    private readonly TextElement _help;
    private readonly TextElement _rotateLeft;
    private readonly TextElement _rotateRight;
    private readonly TextElement _newGame;
    private readonly ImageElement _background;
    private readonly ImageElement _field;
    private Vector2D<int> _size;
    private Vector2D<float> _pointer = new(-1, -1);
    private float _left, _top, _cell, _trayLeft, _trayTop, _slotWidth;
    private int _selected;
    private bool _confirmRestart;
    private long _layerRevision = -1;

    public LandfallScene(ITextStyleRegistry styles, IEventHub events,
        IWindowService windows, ITextureRegistry textures)
    {
        _windows = windows;
        _events = events;
        _styles = styles;
        _atlas = textures.GetOrCreate("terrain-tile-map");
        MainCamera = _camera;
        _boardRenderer = Renderer(_board, _atlas, 0);
        _resourceRenderer = Renderer(_resources, textures.GetOrCreate("resource-tile-map"), 1);
        _buildingRenderer = Renderer(_buildings, _atlas, 2);
        // Fog masks every map layer; grid lines and placement previews remain above it.
        _fogRenderer = Renderer(_fog, BuiltInTextures.Uniform, 3);
        _previewRenderer = Renderer(_preview, _atlas, 10);
        _boardObject = new([_boardRenderer]);
        _fogObject = new([_fogRenderer]);
        _resourceObject = new([_resourceRenderer]);
        _buildingObject = new([_buildingRenderer]);
        _previewObject = new([_previewRenderer]);
        _background = Panel(Color.FromArgb(0xff080c0a), -100);
        _field = Panel(Color.FromArgb(0xff151a17), -30);
        _status = Label("", 18);
        _help = Label("Select 1-5 or click a piece | Q/E or R: rotate | Click board: place | Esc: deselect", 16);
        _rotateLeft = Label("[ Q: Rotate left ]", 17);
        _rotateRight = Label("[ E/R: Rotate right ]", 17);
        _newGame = Label("[ N: New game ]", 17);
        for (var i = 0; i < 5; i++)
        {
            _trayMaps[i] = new(new(-5, -5, 11, 11));
            _trayRenderers[i] = Renderer(_trayMaps[i], _atlas, 0);
            _trayObjects[i] = new([_trayRenderers[i]]);
            _cards[i] = Panel(Colors.DarkGray, -20);
            _labels[i] = Label("", 16);
        }
        for (var i = 0; i < _gridLines.Length; i++)
            _gridLines[i] = Panel(Color.FromArgb(0x5527352d), 5);

        InputMap = new InputMap(events);
        foreach (var key in new[] { KeyEnum.Number1, KeyEnum.Number2, KeyEnum.Number3,
            KeyEnum.Number4, KeyEnum.Number5, KeyEnum.Q, KeyEnum.E, KeyEnum.R,
            KeyEnum.N, KeyEnum.Enter, KeyEnum.Escape })
        {
            InputMap.OnKeyPressed(key).Invoke(() => { if (_held.Add(key)) KeyPressed(key); });
            InputMap.OnKeyReleased(key).Invoke(() => _held.Remove(key));
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        Children.Add(new View { Camera = _camera, PreserveDrawOrder = true });
        Children.Add(_background);
        Children.Add(_field);
        Children.Add(_boardObject);
        Children.Add(_resourceObject);
        Children.Add(_buildingObject);
        Children.Add(_fogObject);
        Children.Add(_previewObject);
        foreach (var line in _gridLines) Children.Add(line);
        for (var i = 0; i < 5; i++)
        {
            Children.Add(_cards[i]);
            Children.Add(_trayObjects[i]);
            Children.Add(_labels[i]);
        }
        foreach (var text in new[] { _status, _help, _rotateLeft, _rotateRight, _newGame }) Children.Add(text);
        Children.Add(Label("LANDFALL", 28));
        Layout();
        Refresh();
    }

    public override void Activate()
    {
        if (IsActivated) return;
        base.Activate();
        _events.Register(this);
    }

    public override void Deactivate()
    {
        _events.Unregister(this);
        _held.Clear();
        base.Deactivate();
    }

    public override void Update(double deltaTime)
    {
        if (_windows.GetMainWindow().Size != _size) { Layout(); Refresh(); }
        if (_layerRevision != _game.LayerRevision) Refresh();
        base.Update(deltaTime);
    }

    public void Handle(MouseMovedEvent message)
    {
        _pointer = message.Position;
        UpdatePreview();
    }

    public void Handle(MouseCanceledEvent message)
    {
        _pointer = new(-1, -1);
        _held.Clear();
        UpdatePreview();
    }

    public void Handle(MouseButtonPressedEvent message)
    {
        _pointer = message.Position;
        if (message.Button != MouseButtonEnum.Left) return;
        if (Hit(_newGame, _pointer)) { RequestRestart(); return; }
        if (_confirmRestart) return;
        if (Hit(_rotateLeft, _pointer)) { Rotate(-1); return; }
        if (Hit(_rotateRight, _pointer)) { Rotate(1); return; }
        if (_game.IsGameOver) return;
        for (var i = 0; i < 5; i++)
            if (Hit(_cards[i], _pointer)) { Select(i); return; }
        if (_selected >= 0 && PointerCell() is { } cell)
        {
            if (_game.TryPlace(_selected, cell)) _selected = -1;
            Refresh();
        }
    }

    private void KeyPressed(KeyEnum key)
    {
        if (key == KeyEnum.Escape)
        {
            _confirmRestart = false;
            _selected = -1;
            Refresh();
            return;
        }
        if (key == KeyEnum.N || (_confirmRestart && key == KeyEnum.Enter)) { RequestRestart(); return; }
        if (_confirmRestart || _game.IsGameOver) return;
        if (key >= KeyEnum.Number1 && key <= KeyEnum.Number5) Select(key - KeyEnum.Number1);
        else if (key == KeyEnum.Q) Rotate(-1);
        else if (key is KeyEnum.E or KeyEnum.R) Rotate(1);
    }

    private void Select(int slot)
    {
        _selected = _selected == slot ? -1 : slot;
        Refresh();
    }

    private void Rotate(int turns)
    {
        if (_selected < 0 || _game.IsGameOver || _confirmRestart) return;
        _game.Pieces[_selected].Rotate(turns);
        Refresh();
    }

    private void RequestRestart()
    {
        if (_confirmRestart || _game.IsGameOver)
        {
            _game.Restart();
            _selected = 0;
            _confirmRestart = false;
        }
        else _confirmRestart = true;
        Refresh();
    }

    private void Refresh()
    {
        _board.Clear();
        _fog.Clear();
        _resources.Clear();
        _buildings.Clear();
        _layerRevision = _game.LayerRevision;
        var fogCell = new TileMapCell(new(0, 0, 1, 1), Color.FromArgb(0xff777b78));
        for (var y = 0; y < 20; y++)
        for (var x = 0; x < 20; x++)
        {
            if (_game[x, y] is { } terrain)
                _board.SetCell(x, y, x is 9 or 10 && y is 9 or 10
                    ? new TileMapCell(new(0, 0, 1 / 8f, 1 / 8f), Colors.White)
                    : Tile(terrain, Colors.White));
            if (!_game.IsRevealed(x, y)) _fog.SetCell(x, y, fogCell);
            if (_game.IsResourceVisible(x, y) && _game.GetResource(x, y) is { } resource)
                _resources.SetCell(x, y, FeatureTile(resource));
            if (_game.IsRevealed(x, y) && _game.GetBuilding(x, y) is { } building)
                _buildings.SetCell(x, y, FeatureTile(building));
        }
        for (var i = 0; i < 5; i++)
        {
            var piece = _game.Pieces[i];
            _trayMaps[i].Clear();
            foreach (var tile in piece.Tiles) _trayMaps[i].SetCell(tile.X, tile.Y, Tile(piece.Terrain, Colors.White));
            _cards[i].Color = Color.FromArgb(i == _selected ? 0xff708961u : 0xff293c32u);
            _labels[i].Text = $"{i + 1}  {piece.Terrain}";
            var tiles = piece.Tiles;
            var centerX = (tiles.Min(c => c.X) + tiles.Max(c => c.X) + 1) / 2f;
            var centerY = (tiles.Min(c => c.Y) + tiles.Max(c => c.Y) + 1) / 2f;
            var scale = _trayRenderers[i].CellSize.X;
            _trayObjects[i].Position = new(_trayLeft + (i + 0.5f) * _slotWidth - centerX * scale,
                _trayTop + 65 - centerY * scale);
        }
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        _preview.Clear();
        if (_confirmRestart)
        {
            _status.Text = "Clear this landscape? Press N / Enter or click New game again. Esc cancels.";
            return;
        }
        if (_game.IsGameOver)
        {
            _status.Text = "No placements remain. Press N or click New game to start a new landscape.";
            return;
        }
        _status.Text = _selected < 0 ? "Select a terrain piece." : "Place a piece beside the landscape. The anchor tile stays under the pointer.";
        if (_selected < 0 || PointerCell() is not { } anchor) return;
        var piece = _game.Pieces[_selected];
        var validation = _game.Validate(piece.Tiles, anchor);
        var valid = validation == PlacementResult.Valid;
        foreach (var tile in piece.Tiles)
        {
            var destination = tile + anchor;
            _preview.SetCell(destination.X, destination.Y,
                Tile(piece.Terrain, (valid ? Colors.White : Colors.Red).WithTransparency(0.65f)));
        }
        _status.Text = validation switch
        {
            PlacementResult.Valid => "Valid placement - click to place.",
            PlacementResult.OutsideBoard => "Outside the board.",
            PlacementResult.Overlap => "Overlaps existing terrain.",
            _ => "Must share an edge with existing terrain.",
        };
    }

    private Cell? PointerCell()
    {
        if (_cell <= 0) return null;
        var x = (int)MathF.Floor((_pointer.X - _left) / _cell);
        var y = (int)MathF.Floor((_pointer.Y - _top) / _cell);
        var cell = new Cell(x, y);
        return LandfallGame.InBounds(cell) ? cell : null;
    }

    private void Layout()
    {
        _size = _windows.GetMainWindow().Size;
        var side = MathF.Max(20, MathF.Min(_size.X - 40, _size.Y - 300));
        _cell = side / 20;
        _left = (_size.X - side) / 2;
        _top = 66;
        _boardObject.Position = _previewObject.Position = _fogObject.Position = new(_left, _top);
        _resourceObject.Position = _buildingObject.Position = new(_left, _top);
        _boardRenderer.CellSize = _previewRenderer.CellSize = _fogRenderer.CellSize = new(_cell, _cell);
        _resourceRenderer.CellSize = _buildingRenderer.CellSize = new(_cell, _cell);
        Place(_background, 0, 0, Math.Max(1, _size.X), Math.Max(1, _size.Y));
        Place(_field, _left, _top, side, side);
        for (var i = 0; i <= 20; i++)
        {
            Place(_gridLines[i], _left + i * _cell, _top, 1, side);
            Place(_gridLines[21 + i], _left, _top + i * _cell, side, 1);
        }
        var trayWidth = MathF.Min(800, MathF.Max(100, _size.X - 40));
        _slotWidth = trayWidth / 5;
        _trayLeft = (_size.X - trayWidth) / 2;
        _trayTop = _top + side + 36;
        for (var i = 0; i < 5; i++)
        {
            Place(_cards[i], _trayLeft + i * _slotWidth + 3, _trayTop, _slotWidth - 6, 116);
            Place(_labels[i], _trayLeft + i * _slotWidth + 10, _trayTop + 7, _slotWidth - 18, 24);
            var scale = MathF.Max(5, MathF.Min(15, (_slotWidth - 22) / 5));
            _trayRenderers[i].CellSize = new(scale, scale);
        }
        Place(_status, 20, _top + side + 7, MathF.Max(1, _size.X - 40), 27);
        var controlsTop = _trayTop + 124;
        Place(_rotateLeft, _trayLeft, controlsTop, trayWidth / 3, 26);
        Place(_rotateRight, _trayLeft + trayWidth / 3, controlsTop, trayWidth / 3, 26);
        Place(_newGame, _trayLeft + 2 * trayWidth / 3, controlsTop, trayWidth / 3, 26);
        Place(_help, 20, controlsTop + 34, MathF.Max(1, _size.X - 40), 26);
    }

    private TextElement Label(string text, int size) => new()
    {
        Text = text, Style = _styles.GetOrCreate(BuiltInFonts.Default, size),
        Color = Colors.WhiteSmoke, HorizontalAlignment = AlignHorizontal.Left,
        VerticalAlignment = AlignVertical.Top, Margins = new(20, 0, 16, 0), SortOrder = 20,
    };

    private static ImageElement Panel(Color color, int order) => new()
    {
        Texture = BuiltInTextures.Uniform, SizingMode = ImageSizingMode.Stretch,
        Color = color, SortOrder = order, HorizontalAlignment = AlignHorizontal.Left,
        VerticalAlignment = AlignVertical.Top,
    };

    private static void Place(Element element, float x, float y, float width, float height)
    {
        element.Margins = new(MathF.Max(0, x), 0, MathF.Max(0, y), 0);
        element.Width = MathF.Max(1, width);
        element.Height = MathF.Max(1, height);
    }

    private static bool Hit(Element element, Vector2D<float> point) =>
        point.X >= element.Bounds.Origin.X && point.Y >= element.Bounds.Origin.Y
        && point.X < element.Bounds.Origin.X + element.Bounds.Size.X
        && point.Y < element.Bounds.Origin.Y + element.Bounds.Size.Y;

    private static TileMapRenderer Renderer(TileMapData map, ITexture texture, int order) => new()
    {
        Map = map, Texture = texture, DrawOrder = order, SamplingBehavior = SamplingBehaviors.PixelPerfect,
    };

    private static TileMapCell FeatureTile(TileFeature feature) => new(
        new(feature.AtlasTile.X / 8f, feature.AtlasTile.Y / 8f, 1 / 8f, 1 / 8f), Colors.White);

    private static TileMapCell Tile(Terrain terrain, Color tint)
    {
        var (x, y) = terrain switch
        {
            Terrain.Grassland => (3, 0), Terrain.Forest => (7, 0),
            Terrain.Mountain => (2, 4), _ => (3, 6),
        };
        return new(new(x / 8f, y / 8f, 1 / 8f, 1 / 8f), tint);
    }
}
