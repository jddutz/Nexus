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
}
