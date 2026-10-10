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

    public class FactoryProxy : DispatchProxy
    {
        public int Creates;
        public int Releases;
        public Color LastTint;
        public HashSet<DrawableId> Active = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
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
