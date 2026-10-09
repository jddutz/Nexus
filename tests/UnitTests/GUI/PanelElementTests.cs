using Nexus.Graphics;
using Nexus.Graphics.Textures;
using Nexus.GUI.Elements;
using Silk.NET.Maths;

namespace Tests;

public sealed class PanelElementTests
{
    [Fact]
    public void Standalone_panels_use_complete_textures_and_switch_selected_artwork()
    {
        var normal = new Texture("normal", 64, 64, new Color[64 * 64]);
        var selected = new Texture("selected", 64, 64, new Color[64 * 64]);
        var panel = new PanelElement(normal, new(8f, 8f, 8f, 8f), selected);
        panel.Arrange(new(10f, 20f, 200f, 100f));
        var renderer = panel.GetComponent<NinePatchRenderer>()!;
        Assert.Same(normal, renderer.Texture);
        Assert.Equal(new Vector4D<float>(0f, 0f, 1f, 1f), renderer.TexCoord);
        Assert.Equal(panel.Bounds, renderer.Destination);
        Assert.True(renderer.IsVisible);
        panel.IsSelected = true;
        Assert.Same(selected, renderer.Texture);
        Assert.Equal(new Vector4D<float>(0f, 0f, 1f, 1f), renderer.TexCoord);
        panel.IsVisible = false;
        Assert.False(renderer.IsVisible);
    }

    [Fact]
    public void Standalone_selection_without_alternate_artwork_keeps_the_original_texture()
    {
        var texture = new Texture("panel", 64, 64, new Color[64 * 64]);
        var panel = new PanelElement(texture, new(8f, 8f, 8f, 8f));
        panel.Arrange(new(0f, 0f, 100f, 100f));
        panel.IsSelected = true;
        Assert.Same(texture, panel.GetComponent<NinePatchRenderer>()!.Texture);
    }
}
