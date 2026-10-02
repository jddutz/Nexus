namespace Nexus.GUI.Elements;

/// <summary>Lays out, clips, and renders one texture image inside its assigned rectangle.</summary>
public partial class ImageElement : Element
{
    private readonly List<IObservable> _visibilityAncestors = [];

    [Observable]
    private ImageSource _imageSource;

    [Observable(PublicSetter = true)]
    private SizingMode _sizingMode;

    [Observable]
    private AlignHorizontal _horizontalAlignment = AlignHorizontal.Center;

    [Observable]
    private AlignVertical _verticalAlignment = AlignVertical.Center;
    private Vector2D<float>? _customSize;
    private Vector4D<float>? _customTexCoord;

    [Observable]
    private ISamplingBehavior _samplingBehavior = SamplingBehaviors.Smooth;

    [Observable]
    private Color _color = Colors.White;

    [Observable]
    private ulong _renderLayerMask = ulong.MaxValue;
    private TextureComponent? _imageComponent;
    private Rectangle<float>? _layoutBounds;

    private void BeforeImageSourceChanges(ImageSource value) =>
        ArgumentNullException.ThrowIfNull(value);

    private void BeforeSizingModeChanges(SizingMode value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value == SizingMode.Custom && (_customSize is null || _customTexCoord is null))
            throw new InvalidOperationException(
                "Use SetCustomSizingMode to provide a custom size and UV rectangle."
            );
    }

    private void BeforeHorizontalAlignmentChanges(AlignHorizontal value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void BeforeVerticalAlignmentChanges(AlignVertical value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void BeforeSamplingBehaviorChanges(ISamplingBehavior value) =>
        ArgumentNullException.ThrowIfNull(value);

    protected virtual partial void AfterImageSourceChanges(ImageSource previousValue) =>
        InvalidateGeometry();

    protected virtual partial void AfterSizingModeChanges(SizingMode previousValue) =>
        InvalidateGeometry();

    protected virtual partial void AfterHorizontalAlignmentChanges(AlignHorizontal previousValue) =>
        InvalidateGeometry();

    protected virtual partial void AfterVerticalAlignmentChanges(AlignVertical previousValue) =>
        InvalidateGeometry();

    protected virtual partial void AfterSamplingBehaviorChanges(ISamplingBehavior previousValue)
    {
        if (_imageComponent is not null)
            _imageComponent.SamplingBehavior = _samplingBehavior;
    }

    protected virtual partial void AfterColorChanges(Color previousValue)
    {
        if (_imageComponent is not null)
            _imageComponent.Color = _color;
    }

    protected virtual partial void AfterRenderLayerMaskChanges(ulong previousValue)
    {
        if (_imageComponent is not null)
            _imageComponent.RenderLayerMask = _renderLayerMask;
    }

    /// <summary>Gets the custom image size, when configured through <see cref="SetCustomSizingMode" />.</summary>
    public Vector2D<float>? CustomSize => _customSize;

    /// <summary>Gets the custom normalized source rectangle used by custom sizing.</summary>
    public Vector4D<float>? CustomTexCoord => _customTexCoord;

    /// <summary>Initializes an image element with a required texture source.</summary>
    /// <param name="imageSource">The texture and optional pixel-space source rectangle.</param>
    public ImageElement(ImageSource imageSource)
        : base()
    {
        ArgumentNullException.ThrowIfNull(imageSource);
        _imageSource = imageSource;
        UpdateVisualComponent();
    }

    /// <summary>Sets custom sizing and its independent normalized source UV rectangle atomically.</summary>
    /// <param name="customSize">The positive destination tile size in logical units.</param>
    /// <param name="customTexCoord">The positive normalized source rectangle within the texture.</param>
    public void SetCustomSizingMode(Vector2D<float> customSize, Vector4D<float> customTexCoord)
    {
        ValidatePositiveSize(customSize, nameof(customSize));
        ValidateNormalizedTexCoord(customTexCoord, nameof(customTexCoord));

        var configurationChanged = false;
        if (_customSize != customSize)
        {
            _customSize = customSize;
            NotifyPropertyChanged(nameof(CustomSize));
            configurationChanged = true;
        }
        if (_customTexCoord != customTexCoord)
        {
            _customTexCoord = customTexCoord;
            NotifyPropertyChanged(nameof(CustomTexCoord));
            configurationChanged = true;
        }

        if (_sizingMode != SizingMode.Custom)
            SetSizingMode(SizingMode.Custom);
        else if (configurationChanged)
            InvalidateGeometry();
    }

    /// <inheritdoc />
    public override Vector2D<float> Measure(Vector2D<float> constraint)
    {
        ValidateAvailableSize(constraint, nameof(constraint));
        if (!IsEffectivelyVisible || constraint.X == 0f || constraint.Y == 0f)
            return Vector2D<float>.Zero;

        var imageSize = GetImageSize(constraint);
        return new(MathF.Min(imageSize.X, constraint.X), MathF.Min(imageSize.Y, constraint.Y));
    }

    /// <inheritdoc />
    public override void Arrange(Rectangle<float> bounds)
    {
        ValidateBounds(bounds);
        _layoutBounds = bounds;
        base.Arrange(bounds);
        UpdateVisualComponent();
    }

    /// <summary>Invalidates cached quads and reapplies the last parent-assigned rectangle.</summary>
    private void InvalidateGeometry()
    {
        if (_layoutBounds is { } bounds)
            Arrange(bounds);
    }

    /// <summary>Synchronizes visual-component ownership, source data, and actual hit bounds.</summary>
    private void UpdateVisualComponent()
    {
        if (!IsEffectivelyVisible || _layoutBounds is not { } bounds)
        {
            RemoveVisualComponent();
            SetBounds(EmptyBounds);
            return;
        }

        if (bounds.Size.X <= 0f || bounds.Size.Y <= 0f)
        {
            RemoveVisualComponent();
            SetBounds(EmptyBounds);
            return;
        }

        var imageSize = GetImageSize(bounds.Size);
        var imageX = GetHorizontalOffset(bounds.Size.X, imageSize.X);
        var imageY = GetVerticalOffset(bounds.Size.Y, imageSize.Y);
        var left = MathF.Max(0f, imageX);
        var top = MathF.Max(0f, imageY);
        var right = MathF.Min(bounds.Size.X, imageX + imageSize.X);
        var bottom = MathF.Min(bounds.Size.Y, imageY + imageSize.Y);
        if (right <= left || bottom <= top)
        {
            RemoveVisualComponent();
            SetBounds(EmptyBounds);
            return;
        }

        var sourceTexCoord = GetActiveTexCoord();
        var leftFraction = (left - imageX) / imageSize.X;
        var topFraction = (top - imageY) / imageSize.Y;
        var rightFraction = (right - imageX) / imageSize.X;
        var bottomFraction = (bottom - imageY) / imageSize.Y;
        var texCoord = new Vector4D<float>(
            sourceTexCoord.X + leftFraction * sourceTexCoord.Z,
            sourceTexCoord.Y + topFraction * sourceTexCoord.W,
            (rightFraction - leftFraction) * sourceTexCoord.Z,
            (bottomFraction - topFraction) * sourceTexCoord.W
        );

        SetPosition(bounds.Origin);
        SetBounds(
            new Rectangle<float>(
                bounds.Origin.X + left,
                bounds.Origin.Y + top,
                right - left,
                bottom - top
            )
        );
        if (_imageComponent is null)
        {
            _imageComponent = new TextureComponent();
            AddComponent(_imageComponent);
        }

        SynchronizeVisualComponent(
            _imageComponent,
            new Rectangle<float>(
                bounds.Origin.X + left,
                bounds.Origin.Y + top,
                right - left,
                bottom - top
            ),
            texCoord
        );
    }

    /// <summary>Applies retained source and visual settings to the current texture component.</summary>
    /// <param name="component">The owned visual component.</param>
    /// <param name="destination">The clipped visual destination.</param>
    /// <param name="texCoord">The clipped normalized source rectangle.</param>
    private void SynchronizeVisualComponent(
        TextureComponent component,
        Rectangle<float> destination,
        Vector4D<float> texCoord
    )
    {
        component.Texture = _imageSource.Texture;
        component.Destination = destination;
        component.TexCoord = texCoord;
        component.Color = _color;
        component.SamplingBehavior = _samplingBehavior;
        component.RenderLayerMask = _renderLayerMask;
    }

    /// <summary>Removes the current visual component while retaining image configuration.</summary>
    private void RemoveVisualComponent()
    {
        var component = _imageComponent;
        _imageComponent = null;
        if (component is not null)
            RemoveComponent(component);
    }

    /// <summary>Calculates the uncapped image size for the selected sizing mode.</summary>
    /// <param name="availableSize">The assigned rectangle or measure constraint.</param>
    /// <returns>The image size before clipping.</returns>
    private Vector2D<float> GetImageSize(Vector2D<float> availableSize)
    {
        var sourceWidth = _imageSource.SourceRegion.Size.X;
        var sourceHeight = _imageSource.SourceRegion.Size.Y;
        var sourceSize = new Vector2D<float>(sourceWidth, sourceHeight);
        var imageSize = _sizingMode switch
        {
            SizingMode.Original => sourceSize,
            SizingMode.Fit => sourceSize
                * MathF.Min(availableSize.X / sourceSize.X, availableSize.Y / sourceSize.Y),
            SizingMode.FitHorizontal => new(
                availableSize.X,
                sourceSize.Y * availableSize.X / sourceSize.X
            ),
            SizingMode.FitVertical => new(
                sourceSize.X * availableSize.Y / sourceSize.Y,
                availableSize.Y
            ),
            SizingMode.Fill => sourceSize
                * MathF.Max(availableSize.X / sourceSize.X, availableSize.Y / sourceSize.Y),
            SizingMode.Stretch => availableSize,
            SizingMode.Custom => _customSize!.Value,
            _ => throw new InvalidOperationException("The configured sizing mode is invalid."),
        };
        if (
            !float.IsFinite(imageSize.X)
            || !float.IsFinite(imageSize.Y)
            || imageSize.X <= 0f
            || imageSize.Y <= 0f
        )
            throw new InvalidOperationException(
                "The image sizing calculation was not positive and finite."
            );

        return imageSize;
    }

    /// <summary>Gets the horizontal offset for the selected image alignment.</summary>
    /// <param name="availableSize">The assigned rectangle width.</param>
    /// <param name="imageSize">The rendered image width before clipping.</param>
    /// <returns>The image's left edge in element-local coordinates.</returns>
    private float GetHorizontalOffset(float availableSize, float imageSize) =>
        _horizontalAlignment switch
        {
            AlignHorizontal.Left => 0f,
            AlignHorizontal.Center => (availableSize - imageSize) / 2f,
            AlignHorizontal.Right => availableSize - imageSize,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the vertical offset for the selected image alignment.</summary>
    /// <param name="availableSize">The assigned rectangle height.</param>
    /// <param name="imageSize">The rendered image height before clipping.</param>
    /// <returns>The image's top edge in element-local coordinates.</returns>
    private float GetVerticalOffset(float availableSize, float imageSize) =>
        _verticalAlignment switch
        {
            AlignVertical.Top => 0f,
            AlignVertical.Center => (availableSize - imageSize) / 2f,
            AlignVertical.Bottom => availableSize - imageSize,
            _ => throw new InvalidOperationException(),
        };

    /// <summary>Gets the currently selected normalized source rectangle.</summary>
    /// <returns>The custom UV rectangle in custom mode, or the source pixel rectangle normalized.</returns>
    private Vector4D<float> GetActiveTexCoord()
    {
        if (_sizingMode == SizingMode.Custom)
            return _customTexCoord!.Value;

        var region = _imageSource.SourceRegion;
        return new(
            (float)region.Origin.X / _imageSource.Texture.Width,
            (float)region.Origin.Y / _imageSource.Texture.Height,
            (float)region.Size.X / _imageSource.Texture.Width,
            (float)region.Size.Y / _imageSource.Texture.Height
        );
    }

    /// <summary>Subscribes to visibility changes on the current ancestor chain.</summary>
    private void UpdateVisibilityAncestorSubscriptions()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();

        for (ISceneNode? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is not IObservable observable)
                continue;

            observable.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(observable);
        }
    }

    /// <summary>Recreates the image visual when an ancestor's visibility changes.</summary>
    /// <param name="propertyName">The name of the changed property.</param>
    private void OnAncestorPropertyChanged(string propertyName)
    {
        if (propertyName is "" or nameof(IsVisible))
            UpdateVisualComponent();
    }

    /// <inheritdoc />
    public override void OnSceneHierarchyChanged()
    {
        base.OnSceneHierarchyChanged();
        UpdateVisibilityAncestorSubscriptions();
        UpdateVisualComponent();
    }

    /// <inheritdoc />
    protected override void AfterIsVisibleChanges() => UpdateVisualComponent();

    /// <summary>Validates a positive finite logical size.</summary>
    /// <param name="size">The proposed size.</param>
    /// <param name="parameterName">The argument name used for validation errors.</param>
    private static void ValidatePositiveSize(Vector2D<float> size, string parameterName)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Both size dimensions must be positive and finite."
            );
    }

    /// <summary>Validates a positive normalized UV rectangle contained in the texture.</summary>
    /// <param name="texCoord">The proposed UV origin and extent.</param>
    /// <param name="parameterName">The argument name used for validation errors.</param>
    private static void ValidateNormalizedTexCoord(Vector4D<float> texCoord, string parameterName)
    {
        if (
            !float.IsFinite(texCoord.X)
            || !float.IsFinite(texCoord.Y)
            || !float.IsFinite(texCoord.Z)
            || !float.IsFinite(texCoord.W)
            || texCoord.X < 0f
            || texCoord.Y < 0f
            || texCoord.Z <= 0f
            || texCoord.W <= 0f
            || texCoord.X + texCoord.Z > 1f
            || texCoord.Y + texCoord.W > 1f
        )
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The UV rectangle must have positive dimensions and fit within [0, 1]."
            );
    }

    /// <summary>Validates a finite, non-negative available size.</summary>
    /// <param name="size">The available size.</param>
    /// <param name="parameterName">The argument name used for validation errors.</param>
    private static void ValidateAvailableSize(Vector2D<float> size, string parameterName)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0f || size.Y < 0f)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Available dimensions must be finite and non-negative."
            );
    }

    /// <summary>Validates the origin and non-negative finite extents of an assigned rectangle.</summary>
    /// <param name="bounds">The proposed arranged rectangle.</param>
    private static void ValidateBounds(Rectangle<float> bounds)
    {
        ValidateAvailableSize(bounds.Size, nameof(bounds));
        if (
            !float.IsFinite(bounds.Origin.X)
            || !float.IsFinite(bounds.Origin.Y)
            || !float.IsFinite(bounds.Max.X)
            || !float.IsFinite(bounds.Max.Y)
        )
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                "The arranged rectangle must have finite coordinates."
            );
    }

    /// <summary>Gets the empty rectangle used when no image pixels are arranged.</summary>
    private static Rectangle<float> EmptyBounds => new(0f, 0f, 0f, 0f);
}
