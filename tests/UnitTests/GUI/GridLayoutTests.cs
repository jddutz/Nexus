using Nexus.GUI;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

/// <summary>Tests grid track definition configuration.</summary>
public class GridLayoutTests
{
    /// <summary>Verifies track definitions can be assigned with collection expressions.</summary>
    [Fact]
    public void TrackDefinitions_supportCollectionExpressions()
    {
        var layout = new GridLayout
        {
            Rows =
            [
                GridSize.Absolute(40f),
                GridSize.Relative(1f),
                GridSize.Relative(80f),
            ],
            Columns = [GridSize.Relative(1f)],
        };

        Assert.Equal(
            [
                GridSize.Absolute(40f),
                GridSize.Relative(1f),
                GridSize.Relative(80f),
            ],
            layout.Rows
        );
        Assert.Equal([GridSize.Relative(1f)], layout.Columns);
        Assert.Equal(3, layout.RowCount);
        Assert.Equal(1, layout.ColumnCount);
    }

    /// <summary>Verifies returned definitions cannot mutate layout state.</summary>
    [Fact]
    public void TrackDefinitions_returnCopies()
    {
        var layout = new GridLayout { Rows = [GridSize.Relative()] };
        var rows = layout.Rows;
        rows[0] = GridSize.Absolute(20f);

        Assert.Equal([GridSize.Relative()], layout.Rows);
    }

    /// <summary>Verifies grid sizes support named constructor arguments.</summary>
    [Fact]
    public void GridSizeConstructor_acceptsNamedArguments()
    {
        var size = new GridSize(mode: GridSizeMode.Relative, value: 2f);

        Assert.Equal(GridSizeMode.Relative, size.Mode);
        Assert.Equal(2f, size.Value);
    }

    /// <summary>Verifies invalid grid size modes and values are rejected.</summary>
    [Fact]
    public void GridSizeConstructor_rejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridSize((GridSizeMode)99, 1f)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridSize(GridSizeMode.Relative)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridSize(GridSizeMode.Absolute, -1f)
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridSize(GridSizeMode.Auto, 1f)
        );
    }

    /// <summary>Verifies cells receive the lengths allocated to their row and column tracks.</summary>
    [Fact]
    public void Arrange_allocatesTrackLengthsToCells()
    {
        var layout = new GridLayout
        {
            Rows = [GridSize.Absolute(10f), GridSize.Relative()],
            Columns = [GridSize.Relative(1f), GridSize.Relative(3f)],
        };
        var first = new Element();
        var second = new Element();
        var third = new Element();
        var fourth = new Element();
        layout.SetCell(0, 0, first);
        layout.SetCell(0, 1, second);
        layout.SetCell(1, 0, third);
        layout.SetCell(1, 1, fourth);

        layout.Arrange(new Rectangle<float>(0f, 0f, 80f, 50f));

        Assert.Equal(new Rectangle<float>(0f, 0f, 20f, 10f), first.Bounds);
        Assert.Equal(new Rectangle<float>(20f, 0f, 60f, 10f), second.Bounds);
        Assert.Equal(new Rectangle<float>(0f, 10f, 20f, 40f), third.Bounds);
        Assert.Equal(new Rectangle<float>(20f, 10f, 60f, 40f), fourth.Bounds);
    }

    /// <summary>Verifies auto tracks include measured occupant margins before relative allocation.</summary>
    [Fact]
    public void Arrange_autoColumnsMeasureContentAndMarginsBeforeRelativeTracks()
    {
        var layout = new GridLayout
        {
            Rows = [GridSize.Absolute(20f)],
            Columns =
            [
                GridSize.Absolute(10f),
                GridSize.Auto,
                GridSize.Absolute(15f),
                GridSize.Relative(),
            ],
        };
        layout.SetCell(
            0,
            1,
            new MeasuredElement(new(30f, 12f)) { Margins = new Margins(2f, 3f, 4f, 5f) }
        );
        var relativeOccupant = new Element();
        layout.SetCell(0, 3, relativeOccupant);

        layout.Arrange(new Rectangle<float>(0f, 0f, 100f, 20f));

        Assert.Equal(new Rectangle<float>(60f, 0f, 40f, 20f), relativeOccupant.Bounds);
    }

    /// <summary>Verifies auto rows are measured after the maximum auto-column width is resolved.</summary>
    [Fact]
    public void MeasureAndArrange_autoRowsUseResolvedColumnWidths()
    {
        var layout = new GridLayout
        {
            Rows = [GridSize.Auto, GridSize.Relative()],
            Columns = [GridSize.Auto],
        };
        layout.SetCell(
            0,
            0,
            new MeasuredElement(new(40f, 0f), width => width < 40f ? 35f : 20f)
            {
                Margins = new Margins(2f, 3f, 4f, 5f),
            }
        );
        layout.SetCell(1, 0, new MeasuredElement(new(60f, 10f)));

        Assert.Equal(new Vector2D<float>(60f, 100f), layout.Measure(new(100f, 100f)));
        layout.Arrange(new Rectangle<float>(0f, 0f, 100f, 100f));

        Assert.Equal(29f, layout.GetCell(1, 0)!.Bounds.Origin.Y);
    }

    /// <summary>Verifies resizing track definitions preserves cells that remain in range.</summary>
    [Fact]
    public void TrackDefinitions_resizeCellsAndRetainOverlappingOccupants()
    {
        var occupant = new Element();
        var layout = new GridLayout
        {
            Rows = [GridSize.Relative()],
            Columns = [GridSize.Relative(), GridSize.Relative()],
        };
        layout.SetCell(0, 0, occupant);

        layout.Columns = [GridSize.Relative()];
        Assert.Same(occupant, layout.GetCell(0, 0));
        layout.Columns = [GridSize.Relative(), GridSize.Relative()];

        Assert.Same(occupant, layout.GetCell(0, 0));
        Assert.Null(layout.GetCell(0, 1));
    }

    /// <summary>Measures configured content while accounting for the element's margins.</summary>
    private sealed class MeasuredElement : Element
    {
        private readonly Vector2D<float> _desiredSize;
        private readonly Func<float, float>? _heightForWidth;

        /// <summary>Initializes a measurable test element.</summary>
        /// <param name="desiredSize">The desired content size.</param>
        /// <param name="heightForWidth">An optional content-height function of available width.</param>
        public MeasuredElement(
            Vector2D<float> desiredSize,
            Func<float, float>? heightForWidth = null
        )
        {
            _desiredSize = desiredSize;
            _heightForWidth = heightForWidth;
        }

        /// <inheritdoc/>
        public override Vector2D<float> Measure(Vector2D<float> constraint)
        {
            var contentWidth = MathF.Max(0f, constraint.X - Margins.Left - Margins.Right);
            var contentHeight = MathF.Max(0f, constraint.Y - Margins.Top - Margins.Bottom);
            var desiredHeight = _heightForWidth?.Invoke(contentWidth) ?? _desiredSize.Y;
            return new Vector2D<float>(
                    MathF.Min(_desiredSize.X, contentWidth),
                    MathF.Min(desiredHeight, contentHeight)
                )
                + Margins;
        }
    }
}
