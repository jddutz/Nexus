namespace Tests;

using Nexus.Core;
using Nexus.Graphics.Shaders;

/// <summary>Reads drawable data through the public rendering contracts for tests.</summary>
internal static class DrawableTestData
{
    /// <summary>Writes every instance into a buffer sized from the shader layout.</summary>
    /// <param name="drawable">The drawable to serialize.</param>
    /// <param name="layout">The required instance layout.</param>
    /// <returns>The packed instance records.</returns>
    public static ReadOnlyMemory<byte> ReadInstances(IDrawable drawable, ShaderInput[] layout)
    {
        var stride = checked(layout.Sum(input => input.Size));
        var data = new byte[checked(stride * (int)drawable.InstanceCount)];
        drawable.WriteInstanceDataTo(0, drawable.InstanceCount, layout, data);
        return data;
    }

    /// <summary>Writes one uniform block into a buffer sized from the shader layout.</summary>
    /// <param name="drawable">The drawable to serialize.</param>
    /// <param name="layout">The required uniform layout.</param>
    /// <returns>The packed uniform block.</returns>
    public static ReadOnlyMemory<byte> ReadUniform(IDrawable drawable, ShaderInput[] layout)
    {
        var data = new byte[checked(layout.Sum(input => input.Size))];
        drawable.WriteUniformDataTo(0, 1, layout, data);
        return data;
    }

    /// <summary>Finds the graphics component owning an element's text drawable.</summary>
    /// <param name="element">The element whose visual components are inspected.</param>
    /// <returns>The unique text graphics component.</returns>
    public static IRenderer TextGraphics(IGameObject element) =>
        Assert.Single(
            element.Components.OfType<IRenderer>(),
            component => component.Drawables.OfType<TextSpan>().Any()
        );

    /// <summary>Finds the prepared text span owned by an element.</summary>
    /// <param name="element">The element whose text is inspected.</param>
    /// <returns>The unique prepared text span.</returns>
    public static TextSpan TextDrawable(IGameObject element) =>
        Assert.IsType<TextSpan>(Assert.Single(TextGraphics(element).Drawables));

    /// <summary>Gets the number of glyph instances rendered for an element.</summary>
    /// <param name="element">The element whose text drawable is inspected.</param>
    /// <returns>The rendered glyph count.</returns>
    public static ulong TextInstanceCount(IGameObject element) =>
        TextDrawable(element).InstanceCount;
}
