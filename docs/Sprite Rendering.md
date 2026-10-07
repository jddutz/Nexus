# Sprite rendering

A `SpriteRenderer` renders its visible instances through one `SpriteDrawable`.
Texture, sampling behavior, shaders, layer mask, and draw order are shared.
Separate renderers are not automatically batched. All instances occupy one
position in draw ordering; use separate renderer groups when sprites must
interleave with other drawables.

```csharp
var renderer = new SpriteRenderer { Texture = atlas };
var sprite = new SpriteInstance
{
    Size = new(32f, 48f),
    Anchor = new(0.5f, 1f),
    Transform = Matrix4X4.CreateTranslation(100f, 200f, 0f),
    TexCoord = new(0f, 0f, 0.25f, 0.25f),
};
var id = renderer.Add(sprite);
```

Size describes the local quad before transformation. Anchor is the normalized
point at the instance transform's origin: local origin is `-Anchor * Size`.
For the engine's Y-down screen convention, `(0.5, 1)` is bottom-center.
The packed transform is quad scale, anchor translation, instance transform,
then the owning `IGameObject2D.WorldTransform`, using row-vector composition.
Draw order supplies packed Z, as it does for `TexturedQuad`.
Elevation belongs in combat placement code and changes the instance transform.

Each GPU record uses the existing textured-quad shader layout: a 64-byte
transform, 16-byte texture region, and 16-byte color. Texture regions retain
the `(u, v, width, height)` convention. Use finite transforms, positive sizes,
normalized atlas regions and color channels. Custom shaders must support this
layout and the existing View uniform.

`Instances[id]` retrieves the editable instance. IDs remain stable after another
instance is removed; packed buffer indices are internal. Edits invalidate the
existing drawable's instance data. Remove and Clear unsubscribe from instance
notifications. Renderer visibility hides the entire collection; instance
visibility filters only that sprite. Empty or fully hidden collections expose
no drawable, as do renderers missing a texture, sampler, or shader.

`SpriteAnimation` stores a copied, nonempty sequence of `SpriteFrame` records
with positive durations. `SpriteAnimationPlayer.Advance(TimeSpan)` changes only
its target instance's texture region. Advance each player from gameplay update
code; looping wraps at the cycle boundary and nonlooping playback holds its
final frame. `Restart()` assigns the first frame again. Frames use a consistent
size and anchor; trimmed and rotated atlas frames are not supported.
