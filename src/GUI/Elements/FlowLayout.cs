using Nexus.Core;

namespace Nexus.GUI.Elements;

/// <summary>Arranges child elements sequentially into rows or columns.</summary>
public partial class FlowLayout : Element
{
    [Observable]
    private LayoutPriority _priority = LayoutPriority.Horizontal;

    [Observable]
    private DirectionHorizontal _horizontalDirection = DirectionHorizontal.LeftToRight;

    [Observable]
    private DirectionVertical _verticalDirection = DirectionVertical.Down;

    [Observable]
    private LayoutSizingMode _layoutMode = LayoutSizingMode.Fit;

    [Observable]
    private uint _size = 1u;

    [Observable]
    private ItemSize _itemSize = ItemSize.Unbounded;

    [Observable]
    private ItemSpacing _itemSpacing = ItemSpacing.None;

    /// <summary>Initializes a flow layout and subscribes to child changes.</summary>
    public FlowLayout()
    {
        Children.ItemAdded += OnChildAdded;
        Children.ItemRemoved += OnChildRemoved;
    }

    /// <summary>Sets the layout priority after validating the enum value.</summary>
    protected virtual void SetPriority(LayoutPriority value)
    {
        ValidateEnum(value, nameof(value));
        _priority = value;
    }

    /// <summary>Sets the horizontal direction after validating the enum value.</summary>
    protected virtual void SetHorizontalDirection(DirectionHorizontal value)
    {
        ValidateEnum(value, nameof(value));
        _horizontalDirection = value;
    }

    /// <summary>Sets the vertical direction after validating the enum value.</summary>
    protected virtual void SetVerticalDirection(DirectionVertical value)
    {
        ValidateEnum(value, nameof(value));
        _verticalDirection = value;
    }

    /// <summary>Sets the line sizing mode after validating the enum value.</summary>
    protected virtual void SetLayoutMode(LayoutSizingMode value)
    {
        ValidateEnum(value, nameof(value));
        _layoutMode = value;
    }

    /// <summary>Sets the line count or size after validating the value.</summary>
    protected virtual void SetSize(uint value)
    {
        if (value == 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        _size = value;
    }

    /// <summary>Invalidates layout after a flow property changes.</summary>
    protected virtual partial void AfterPriorityChanges() => InvalidateLayout();

    /// <summary>Invalidates layout after a horizontal direction changes.</summary>
    protected virtual partial void AfterHorizontalDirectionChanges() => InvalidateLayout();

    /// <summary>Invalidates layout after a vertical direction changes.</summary>
    protected virtual partial void AfterVerticalDirectionChanges() => InvalidateLayout();

    /// <summary>Invalidates layout after the line sizing mode changes.</summary>
    protected virtual partial void AfterLayoutModeChanges() => InvalidateLayout();

    /// <summary>Invalidates layout after the line size changes.</summary>
    protected virtual partial void AfterSizeChanges() => InvalidateLayout();

    /// <summary>Invalidates layout after item sizing changes.</summary>
    protected virtual partial void AfterItemSizeChanges() => InvalidateLayout();

    /// <summary>Invalidates layout after item spacing changes.</summary>
    protected virtual partial void AfterItemSpacingChanges() => InvalidateLayout();

    /// <inheritdoc />
    protected override void AfterMarginsChanges()
    {
        base.AfterMarginsChanges();
        InvalidateLayout();
    }

    /// <inheritdoc />
    protected override void AfterIsVisibleChanges()
    {
        base.AfterIsVisibleChanges();
        InvalidateLayout();
    }

    /// <summary>Invalidates layout after the explicit width changes.</summary>
    protected override void AfterWidthChanges() => InvalidateLayout();

    /// <summary>Invalidates layout after the explicit height changes.</summary>
    protected override void AfterHeightChanges() => InvalidateLayout();

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateSize(constraint);
        if (!IsEffectivelyVisible)
            return Vector2D<float>.Zero;

        var available = GetContentConstraint(constraint);
        var plan = BuildPlan(available);
        var occupied = new Vector2D<float>(plan.PrimaryExtent, plan.CrossExtent);
        if (Priority == LayoutPriority.Vertical)
            occupied = new Vector2D<float>(plan.CrossExtent, plan.PrimaryExtent);

        var desired = new Vector2D<float>(
            MathF.Min(Width ?? occupied.X, available.X),
            MathF.Min(Height ?? occupied.Y, available.Y)
        );
        return IncludeMargins(desired);
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        ValidateSize(bounds.Size);
        if (!float.IsFinite(bounds.Origin.X) || !float.IsFinite(bounds.Origin.Y))
            throw new ArgumentOutOfRangeException(nameof(bounds));

        if (!IsEffectivelyVisible)
        {
            SetBounds(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
            ArrangeEmptyChildren(bounds.Origin);
            return;
        }

        var available = GetContentBounds(bounds);
        var desired = new Vector2D<float>(
            MathF.Min(Width ?? available.Size.X, available.Size.X),
            MathF.Min(Height ?? available.Size.Y, available.Size.Y)
        );
        var destination = GetAlignedContentBounds(bounds, desired);
        SetBounds(destination);
        var content = new Rectangle<float>(
            destination.Origin,
            destination.Size
        );
        var plan = BuildPlan(content.Size);
        ArrangePlan(plan, content);
    }

    /// <summary>Subscribes to an added child's changes and invalidates this layout.</summary>
    /// <param name="node">The child that was added.</param>
    private void OnChildAdded(ISceneNode node)
    {
        if (node is IGameObject gameObject)
            gameObject.PropertyChanged += OnChildPropertyChanged;
        InvalidateLayout();
    }

    /// <summary>Unsubscribes from a removed child's changes and invalidates this layout.</summary>
    /// <param name="node">The child that was removed.</param>
    private void OnChildRemoved(ISceneNode node)
    {
        if (node is IGameObject gameObject)
            gameObject.PropertyChanged -= OnChildPropertyChanged;
        InvalidateLayout();
    }

    /// <summary>Invalidates layout when a child reports a property change.</summary>
    /// <param name="propertyName">The changed child property name.</param>
    private void OnChildPropertyChanged(string propertyName) => InvalidateLayout();

    /// <summary>Clears all child allocations when the layout is hidden.</summary>
    /// <param name="origin">The origin used for empty child rectangles.</param>
    private void ArrangeEmptyChildren(Vector2D<float> origin)
    {
        foreach (var child in Children)
            if (child is IElement element)
                element.Arrange(new Rectangle<float>(origin, Vector2D<float>.Zero));
    }

    /// <summary>Builds a line allocation plan for the supplied interior capacity.</summary>
    /// <param name="available">The available interior capacity.</param>
    /// <returns>The newly computed allocation plan.</returns>
    private LayoutPlan BuildPlan(Vector2D<float> available)
    {
        var primaryCapacity = Priority == LayoutPriority.Horizontal ? available.X : available.Y;
        var crossCapacity = Priority == LayoutPriority.Horizontal ? available.Y : available.X;
        var primaryGap = Priority == LayoutPriority.Horizontal
            ? ItemSpacing.Horizontal
            : ItemSpacing.Vertical;
        var crossGap = Priority == LayoutPriority.Horizontal
            ? ItemSpacing.Vertical
            : ItemSpacing.Horizontal;
        var items = new List<Item>();

        foreach (var child in Children)
        {
            if (child is not IElement element || !element.IsVisible)
                continue;

            var measured = element.Measure(available);
            var width = Clamp(measured.X, ItemSize.MinimumWidth, ItemSize.MaximumWidth);
            var height = Clamp(measured.Y, ItemSize.MinimumHeight, ItemSize.MaximumHeight);
            items.Add(new Item(element, width, height));
        }

        var lines = new List<Line>();
        var index = 0;
        var crossOffset = 0f;
        var lineLimit = LayoutMode == LayoutSizingMode.Fit ? Size : uint.MaxValue;
        while (index < items.Count && lines.Count < lineLimit)
        {
            var remainingCross = MathF.Max(0f, crossCapacity - crossOffset);
            var lineExtent = InitialLineExtent(crossCapacity, remainingCross, crossGap);
            var line = new Line(lineExtent);

            while (index < items.Count)
            {
                var candidate = items[index];
                var previousExtent = line.Extent;
                var proposedExtent = LayoutMode == LayoutSizingMode.Dynamic
                    ? MathF.Min(remainingCross, MathF.Max(line.Extent, CrossDimension(candidate)))
                    : line.Extent;
                line.Extent = proposedExtent;
                line.Items.Add(candidate);
                Recalculate(line, null, proposedExtent, primaryCapacity, primaryGap);
                var required = PrimaryTotal(line, primaryGap);
                if (line.Items.Count == 1 || required <= primaryCapacity)
                {
                    index++;
                    continue;
                }

                line.Items.RemoveAt(line.Items.Count - 1);
                line.Extent = previousExtent;
                Recalculate(line, null, line.Extent, primaryCapacity, primaryGap);
                break;
            }

            if (line.Items.Count == 0 && index < items.Count)
            {
                var item = items[index++];
                line.Items.Add(item);
                Recalculate(line, null, line.Extent, primaryCapacity, primaryGap);
            }

            lines.Add(line);
            crossOffset += line.Extent + crossGap;
            if (crossOffset > crossCapacity && index < items.Count)
                break;
        }

        if (index < items.Count && lines.Count > 0)
        {
            var final = lines[^1];
            while (index < items.Count)
            {
                final.Items.Add(items[index++]);
                Recalculate(final, null, final.Extent, primaryCapacity, primaryGap);
            }
        }

        var crossExtent = lines.Count == 0 ? 0f : MathF.Min(crossCapacity, crossOffset - crossGap);
        var primaryExtent = lines.Count == 0
            ? 0f
            : lines.Max(line => MathF.Min(primaryCapacity, PrimaryTotal(line, primaryGap)));
        return new LayoutPlan(
            lines,
            primaryCapacity,
            primaryExtent,
            crossExtent,
            primaryGap,
            crossGap
        );
    }

    /// <summary>Arranges children using a finalized allocation plan.</summary>
    /// <param name="plan">The plan to apply.</param>
    /// <param name="bounds">The interior bounds receiving the plan.</param>
    private void ArrangePlan(LayoutPlan plan, Rectangle<float> bounds)
    {
        var crossOffset = 0f;
        foreach (var line in plan.Lines)
        {
            var primaryOffset = 0f;
            foreach (var item in line.Items)
            {
                var primary = MathF.Min(item.Primary, MathF.Max(0f, plan.PrimaryCapacity - primaryOffset));
                var cross = MathF.Min(item.Cross, line.Extent);
                var width = Priority == LayoutPriority.Horizontal ? primary : cross;
                var height = Priority == LayoutPriority.Horizontal ? cross : primary;
                var u = Priority == LayoutPriority.Horizontal ? primaryOffset : crossOffset;
                var v = Priority == LayoutPriority.Horizontal ? crossOffset : primaryOffset;
                var x = HorizontalDirection == DirectionHorizontal.LeftToRight
                    ? bounds.Origin.X + u
                    : bounds.Max.X - u - width;
                var y = VerticalDirection == DirectionVertical.Down
                    ? bounds.Origin.Y + v
                    : bounds.Max.Y - v - height;
                item.Element.Arrange(new Rectangle<float>(x, y, width, height));
                primaryOffset += primary + plan.PrimaryGap;
            }

            crossOffset += line.Extent + plan.CrossGap;
        }

        foreach (var child in Children)
            if (child is IElement element && !plan.Contains(element))
                element.Arrange(new Rectangle<float>(bounds.Origin, Vector2D<float>.Zero));
    }

    /// <summary>Recalculates requested item extents for a line extent.</summary>
    /// <param name="line">The line being recalculated.</param>
    /// <param name="candidate">An optional candidate item for a tentative calculation.</param>
    /// <param name="crossExtent">The proposed cross-axis extent.</param>
    /// <param name="primaryCapacity">The available primary-axis capacity.</param>
    /// <param name="primaryGap">The gap between primary-axis items.</param>
    private void Recalculate(
        Line line,
        Item? candidate,
        float crossExtent,
        float primaryCapacity,
        float primaryGap
    )
    {
        var all = candidate is null ? line.Items : [.. line.Items, candidate];
        foreach (var item in all)
        {
            var width = Clamp(item.Width, ItemSize.MinimumWidth, ItemSize.MaximumWidth);
            var height = Clamp(item.Height, ItemSize.MinimumHeight, ItemSize.MaximumHeight);
            if (ItemSize.MaintainAspectRatio && item.Width > 0f && item.Height > 0f)
            {
                var minimumScale = MathF.Max(
                    ItemSize.MinimumWidth / item.Width,
                    ItemSize.MinimumHeight / item.Height
                );
                var maximumScale = MathF.Min(
                    ItemSize.MaximumWidth / item.Width,
                    ItemSize.MaximumHeight / item.Height
                );
                var scale = Priority == LayoutPriority.Horizontal
                    ? crossExtent / item.Height
                    : crossExtent / item.Width;
                if (minimumScale <= maximumScale)
                {
                    scale = Math.Clamp(scale, minimumScale, maximumScale);
                    width = item.Width * scale;
                    height = item.Height * scale;
                }
            }

            item.Primary = Priority == LayoutPriority.Horizontal ? width : height;
            item.Cross = Priority == LayoutPriority.Horizontal ? height : width;
            if (item.Cross > crossExtent)
            {
                item.Cross = crossExtent;
                if (!ItemSize.MaintainAspectRatio)
                    item.Primary = MathF.Min(item.Primary, primaryCapacity);
            }
        }
    }

    /// <summary>Calculates the initial extent for a new line.</summary>
    /// <param name="totalCross">The complete cross-axis capacity.</param>
    /// <param name="remainingCross">The remaining cross-axis capacity.</param>
    /// <param name="gap">The cross-axis gap.</param>
    /// <returns>The bounded proposed line extent.</returns>
    private float InitialLineExtent(float totalCross, float remainingCross, float gap)
    {
        return LayoutMode switch
        {
            LayoutSizingMode.Fixed => MathF.Min(Size, remainingCross),
            LayoutSizingMode.Fit => MathF.Max(
                0f,
                (totalCross - gap * (Size - 1)) / Size
            ),
            LayoutSizingMode.Dynamic => 0f,
            _ => throw new InvalidOperationException("The layout sizing mode is invalid."),
        };
    }

    /// <summary>Calculates the primary-axis extent occupied by a line.</summary>
    /// <param name="line">The line to measure.</param>
    /// <param name="gap">The gap between items.</param>
    /// <returns>The occupied primary-axis extent.</returns>
    private static float PrimaryTotal(Line line, float gap) =>
        MathF.Max(0f, line.Items.Sum(item => item.Primary) + gap * Math.Max(0, line.Items.Count - 1));

    /// <summary>Gets an item's requested cross-axis dimension.</summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>The requested cross-axis dimension.</returns>
    private float CrossDimension(Item item) =>
        Priority == LayoutPriority.Horizontal ? item.Height : item.Width;

    /// <summary>Clamps a dimension to its item-size bounds.</summary>
    /// <param name="value">The requested value.</param>
    /// <param name="minimum">The minimum allowed value.</param>
    /// <param name="maximum">The maximum allowed value.</param>
    /// <returns>The bounded value.</returns>
    private static float Clamp(float value, float minimum, float maximum) =>
        MathF.Min(MathF.Max(value, minimum), maximum);

    /// <summary>Validates a finite, non-negative layout capacity.</summary>
    /// <param name="size">The capacity to validate.</param>
    private static void ValidateSize(Vector2D<float> size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0f || size.Y < 0f)
            throw new ArgumentOutOfRangeException(nameof(size));
    }

    /// <summary>Validates that an enum value is defined.</summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The enum value to validate.</param>
    /// <param name="name">The argument name used in the exception.</param>
    private static void ValidateEnum<T>(T value, string name)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(name);
    }

    private sealed class Item(IElement element, float width, float height)
    {
        public IElement Element { get; } = element;
        public float Width { get; } = width;
        public float Height { get; } = height;
        public float Primary { get; set; }
        public float Cross { get; set; }
    }

    private sealed class Line(float extent)
    {
        public float Extent { get; set; } = extent;
        public List<Item> Items { get; } = [];
    }

    private sealed class LayoutPlan(
        List<Line> lines,
        float primaryCapacity,
        float primaryExtent,
        float crossExtent,
        float primaryGap,
        float crossGap
    )
    {
        public List<Line> Lines { get; } = lines;
        public float PrimaryCapacity { get; } = primaryCapacity;
        public float PrimaryExtent { get; } = primaryExtent;
        public float CrossExtent { get; } = crossExtent;
        public float PrimaryGap { get; } = primaryGap;
        public float CrossGap { get; } = crossGap;

        public bool Contains(IElement element) =>
            Lines.Any(line => line.Items.Any(item => ReferenceEquals(item.Element, element)));
    }
}
