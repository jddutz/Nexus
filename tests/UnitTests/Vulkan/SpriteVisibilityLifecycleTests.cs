using System.Reflection;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Textures;
using Nexus.Graphics.Vulkan;
using Nexus.Graphics.Vulkan.Commands;
using System.Runtime.InteropServices;

namespace Tests;

public class SpriteVisibilityLifecycleTests
{
    [Fact]
    public void ShowingAnOverlayAllocatesOnceAndDoesNotInterruptDamageOrRemoval()
    {
        var factory = DispatchProxy.Create<ICommandFactory, FactoryProxy>();
        var probe = (FactoryProxy)(object)factory;
        var graphics = new VulkanGraphicsSystem(null!, null!, null!, null!, new EventHub(), factory, null!, null!, null!);
        var renderer = new SpriteRenderer
        {
            Texture = new Texture("overlay", 1, 1, [Colors.White]), IsVisible = false,
        };
        var sprite = new SpriteInstance();
        renderer.Add(sprite);
        graphics.Handle(new ComponentActivatedEvent(renderer));
        // DrawableAdded and IsVisibleChanged both reach the graphics system here.
        renderer.IsVisible = true;
        Assert.Equal(1, probe.Creates);
        sprite.Color = new Color(1f, 0.2f, 0.2f, 1f);
        Assert.Equal(sprite.Color, probe.LastTint);
        renderer.IsVisible = false;
        Assert.Equal(1, probe.Releases);
        Assert.Empty(probe.Active);
        renderer.IsVisible = true;
        Assert.Equal(2, probe.Creates);
        renderer.IsVisible = false;
        Assert.Equal(2, probe.Releases);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ViewChangesRebuildOnlyTheChangedViewAndUnsubscribeOnRemoval(bool pipelineChange)
    {
        var factory = DispatchProxy.Create<ICommandFactory, FactoryProxy>();
        var probe = (FactoryProxy)(object)factory;
        var graphics = new VulkanGraphicsSystem(null!, null!, null!, null!, new EventHub(), factory, null!, null!, null!);
        var first = new ViewRenderer { LayerMask = RenderLayers.All };
        var second = new ViewRenderer { LayerMask = RenderLayers.All };
        graphics.RegisterView(first);
        graphics.RegisterView(second);
        var renderer = new SpriteRenderer { Texture = new Texture("overlay", 1, 1, [Colors.White]) };
        renderer.Add(new SpriteInstance());
        graphics.Handle(new ComponentActivatedEvent(renderer));
        probe.ViewCommands.Clear();

        if (pipelineChange) first.EnableDepthTest = !first.EnableDepthTest;
        else first.LayerMask = RenderLayers.All ^ (1UL << 63);
        Assert.NotEmpty(probe.ViewCommands);
        Assert.All(probe.ViewCommands, view => Assert.Same(first, view));

        graphics.UnregisterView(first);
        probe.ViewCommands.Clear();
        first.EnableDepthWrite = !first.EnableDepthWrite;
        Assert.Empty(probe.ViewCommands);
        second.EnableDepthWrite = !second.EnableDepthWrite;
        Assert.NotEmpty(probe.ViewCommands);
        Assert.All(probe.ViewCommands, view => Assert.Same(second, view));
        graphics.UnregisterView(second);
    }

    public class FactoryProxy : DispatchProxy
    {
        public List<ViewRenderer> ViewCommands = [];
        public int Creates;
        public int Releases;
        public Color LastTint;
        public HashSet<DrawableId> Active = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name is nameof(ICommandFactory.ActivateView) or nameof(ICommandFactory.ReleaseView))
                return null;
            if (method.Name == nameof(ICommandFactory.CreateViewCommands))
                ViewCommands.Add((ViewRenderer)args![1]!);
            var drawable = (IDrawable)args![0]!;
            switch (method!.Name)
            {
                case nameof(ICommandFactory.Create):
                    if (!Active.Add(drawable.Id)) throw new InvalidOperationException("Duplicate drawable allocation.");
                    Creates++;
                    break;
                case nameof(ICommandFactory.Release):
                    Assert.True(Active.Remove(drawable.Id));
                    Releases++;
                    break;
                case nameof(ICommandFactory.UpdateInstanceData):
                    var data = new byte[96];
                    drawable.WriteInstanceDataTo(0, 1, drawable.VertexShader!.InstanceLayout, data);
                    LastTint = MemoryMarshal.Read<Color>(data.AsSpan(80));
                    break;
            }
            return Array.Empty<IVulkanCommand>();
        }
    }
}
