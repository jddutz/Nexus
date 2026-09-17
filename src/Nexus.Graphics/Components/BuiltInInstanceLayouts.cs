namespace Nexus.Graphics.Components;

/// <summary>
/// Provides instance-record layouts used by the built-in renderers and shaders.
/// </summary>
public static class BuiltInInstanceLayouts
{
    /// <summary>
    /// Gets the transform-and-color layout consumed by <c>uniform_color.vert</c>.
    /// </summary>
    public static InstanceLayout UniformColor { get; } =
        new(
            stride: 80,
            [
                new(1, 0, InstanceInputFormatEnum.Float4),
                new(2, 16, InstanceInputFormatEnum.Float4),
                new(3, 32, InstanceInputFormatEnum.Float4),
                new(4, 48, InstanceInputFormatEnum.Float4),
                new(5, 64, InstanceInputFormatEnum.Float4),
            ]
        );

    /// <summary>
    /// Gets the transform, texture-region, and color layout consumed by <c>textured_quad.vert</c>.
    /// </summary>
    public static InstanceLayout TexturedQuad { get; } =
        new(
            stride: 96,
            [
                new(2, 0, InstanceInputFormatEnum.Float4),
                new(3, 16, InstanceInputFormatEnum.Float4),
                new(4, 32, InstanceInputFormatEnum.Float4),
                new(5, 48, InstanceInputFormatEnum.Float4),
                new(6, 64, InstanceInputFormatEnum.Float4),
                new(7, 80, InstanceInputFormatEnum.Float4),
            ]
        );
}
