namespace Nexus.GUI.Elements;

/// <summary>Allocates GUI children around a game object's projected world position.</summary>
public class ViewAnnotation : Element
{
    /// <summary>Gets or sets the spatial game object whose world origin anchors this annotation.</summary>
    public IGameObject? TargetGameObject { get; set; }

    /// <summary>Gets or sets the view through which the target is projected.</summary>
    public View? View { get; set; }

    /// <summary>Gets or sets the signed pixel offset of the rectangle's left edge.</summary>
    public float LeftOffset { get; set; }

    /// <summary>Gets or sets the signed pixel offset of the rectangle's right edge.</summary>
    public float RightOffset { get; set; }

    /// <summary>Gets or sets the signed pixel offset of the rectangle's top edge.</summary>
    public float TopOffset { get; set; }

    /// <summary>Gets or sets the signed pixel offset of the rectangle's bottom edge.</summary>
    public float BottomOffset { get; set; }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        var rectangle = new Rectangle<float>(0f, 0f, 0f, 0f);
        if (TargetGameObject is ISpatialObject spatial && View is { } view)
        {
            var world = spatial.WorldTransform;
            if (view.TryProjectToScreen(new(world.M41, world.M42, world.M43), out var point)
                && float.IsFinite(LeftOffset) && float.IsFinite(RightOffset)
                && float.IsFinite(TopOffset) && float.IsFinite(BottomOffset)
                && RightOffset >= LeftOffset && BottomOffset >= TopOffset)
                rectangle = new(point.X + LeftOffset, point.Y + TopOffset,
                    RightOffset - LeftOffset, BottomOffset - TopOffset);
        }

        base.Arrange(rectangle);
    }
}
