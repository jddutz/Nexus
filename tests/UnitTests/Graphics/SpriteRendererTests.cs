using System.Runtime.InteropServices;
using Nexus.Core;
using Nexus.Graphics;
using Nexus.Graphics.Components;
using Nexus.Graphics.Shaders;
using Nexus.Graphics.Textures;
using Silk.NET.Maths;

namespace Nexus.UnitTests.Graphics;

public sealed class SpriteRendererTests
{
    private static SpriteRenderer CreateRenderer() =>
        new() { Texture = new Texture("atlas", 1, 1, [Colors.White]) };

    private static byte[] Read(SpriteRenderer renderer)
    {
        var drawable = Assert.Single(renderer.Drawables);
        var bytes = new byte[checked((int)drawable.InstanceCount * 96)];
        drawable.WriteInstanceDataTo(0, drawable.InstanceCount,
            BuiltInShaders.TexturedQuadVertexShader.InstanceLayout, bytes);
        return bytes;
    }

    [Fact]
    public void PacksAnchoredLocalTransformBeforeOwnerWorldTransform()
    {
        var renderer = CreateRenderer();
        var sprite = new SpriteInstance
        {
            Size = new(2, 4), Anchor = new(0.5f, 1),
            Transform = Matrix4X4.CreateRotationZ(MathF.PI / 2)
                * Matrix4X4.CreateTranslation(3f, 5f, 0f),
        };
        renderer.Add(sprite);
        var owner = new GameObject2D([renderer]) { Position = new(10, 20), Scale = new(2, 3) };
        var expected = Matrix4X4.CreateScale(2f, 4f, 1f)
            * Matrix4X4.CreateTranslation(-1f, -4f, 0f)
            * sprite.Transform * owner.WorldTransform;
        Assert.Equal(expected, MemoryMarshal.Read<Matrix4X4<float>>(Read(renderer)));
        var drawable = Assert.Single(renderer.Drawables);
        var changes = 0;
        drawable.InstanceDataChanged += (_, _) => changes++;
        owner.Position = new(30, 40);
        Assert.True(changes > 0);
        Assert.Same(drawable, Assert.Single(renderer.Drawables));
    }

    [Fact]
    public void EditsAndPackingPreserveDrawableAndStableIds()
    {
        var renderer = CreateRenderer();
        var first = new SpriteInstance();
        var second = new SpriteInstance { TexCoord = new(0.5f, 0, 0.5f, 1) };
        var firstId = renderer.Add(first);
        var secondId = renderer.Add(second);
        var drawable = Assert.Single(renderer.Drawables);
        var added = 0;
        var removed = 0;
        renderer.DrawableAdded += (_, _) => added++;
        renderer.DrawableRemoved += (_, _) => removed++;
        first.Color = new Color(1f, 0f, 0f, 1f);
        second.Size = new(3, 2);
        Assert.Equal(2UL, drawable.InstanceCount);
        Assert.Equal(second.TexCoord, MemoryMarshal.Read<Vector4D<float>>(Read(renderer).AsSpan(160)));
        Assert.True(renderer.Remove(firstId));
        Assert.Same(second, renderer.Instances[secondId]);
        Assert.Same(drawable, Assert.Single(renderer.Drawables));
        Assert.Equal(1UL, drawable.InstanceCount);
        Assert.Equal(0, added);
        Assert.Equal(0, removed);
        var changes = 0;
        drawable.InstanceDataChanged += (_, _) => changes++;
        first.Size = new(9, 9);
        Assert.Equal(0, changes);
        renderer.Clear();
        Assert.Empty(renderer.Drawables);
        second.Size = new(7, 7);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void VisibilityFiltersInstancesAndEmptyRenderersSubmitNothing()
    {
        var renderer = CreateRenderer();
        Assert.Empty(renderer.Drawables);
        var first = new SpriteInstance();
        var second = new SpriteInstance();
        renderer.Add(first);
        renderer.Add(second);
        var drawable = Assert.Single(renderer.Drawables);
        first.IsVisible = false;
        Assert.Same(drawable, Assert.Single(renderer.Drawables));
        Assert.Equal(1UL, drawable.InstanceCount);
        second.IsVisible = false;
        Assert.Empty(renderer.Drawables);
        first.IsVisible = true;
        Assert.Single(renderer.Drawables);
        renderer.IsVisible = false;
        Assert.Empty(renderer.Drawables);
        renderer.IsVisible = true;
        Assert.Equal(1UL, Assert.Single(renderer.Drawables).InstanceCount);
    }

    [Fact]
    public void PartialWritesUsePackedVisibleIndicesAndValidateRanges()
    {
        var renderer = CreateRenderer();
        renderer.Add(new SpriteInstance { IsVisible = false });
        renderer.Add(new SpriteInstance());
        renderer.Add(new SpriteInstance { Color = Colors.Black });
        var drawable = Assert.Single(renderer.Drawables);
        var target = new byte[96];
        drawable.WriteInstanceDataTo(1, 1, BuiltInShaders.TexturedQuadVertexShader.InstanceLayout, target);
        Assert.Equal(Colors.Black, MemoryMarshal.Read<Color>(target.AsSpan(80)));
        drawable.WriteInstanceDataTo(2, 0, [], []);
        Assert.Throws<ArgumentOutOfRangeException>(() => drawable.WriteInstanceDataTo(2, 1, [], target));
        Assert.Throws<ArgumentException>(() => drawable.WriteInstanceDataTo(0, 2,
            BuiltInShaders.TexturedQuadVertexShader.InstanceLayout, target));
    }

    [Fact]
    public void AnimationPlayersAdvanceIndependentlyAndStopOrLoop()
    {
        var renderer = CreateRenderer();
        var first = new SpriteInstance();
        var second = new SpriteInstance();
        renderer.Add(first);
        renderer.Add(second);
        SpriteFrame[] frames = [
            new(new(0, 0, 0.5f, 1), TimeSpan.FromMilliseconds(100)),
            new(new(0.5f, 0, 0.5f, 1), TimeSpan.FromMilliseconds(200)),
        ];
        var looping = new SpriteAnimationPlayer(first, new SpriteAnimation(frames));
        var once = new SpriteAnimationPlayer(second, new SpriteAnimation(frames, false));
        var drawable = Assert.Single(renderer.Drawables);
        looping.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(frames[1].TexCoord, first.TexCoord);
        Assert.Equal(frames[0].TexCoord, second.TexCoord);
        looping.Advance(TimeSpan.FromMilliseconds(800));
        Assert.Equal(frames[0].TexCoord, first.TexCoord);
        once.Advance(TimeSpan.FromSeconds(10));
        Assert.True(once.IsCompleted);
        Assert.Equal(frames[1].TexCoord, second.TexCoord);
        Assert.Same(drawable, Assert.Single(renderer.Drawables));
        once.Restart();
        Assert.False(once.IsCompleted);
        Assert.Throws<ArgumentException>(() => new SpriteAnimation([]));
    }
}
