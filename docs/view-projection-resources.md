# View projection resources

Each active View registers one 64-byte GPU uniform buffer before its drawable commands are built. Descriptor sets are allocated when a pipeline is first selected for that View, using that pipeline's existing set 0 layout. Pure camera sets are reused per pipeline; sets containing drawable bindings are retained per drawable and invalidated when its resources change. Each rendered View records one matrix update, regardless of drawable count.

## Synchronization proof

CommandRecorder submits all rendering command buffers to Context.GraphicsQueue. UpdateUniformBufferCommand's first pipeline barrier orders earlier vertex-shader uniform reads before its transfer write, including reads in earlier submissions on that queue. Its second barrier orders the transfer write before subsequent vertex-shader uniform reads. These barriers cover the 64-byte buffer range. There are no CPU writes to these View buffers. Consequently overlapping frame submissions cannot overwrite an earlier frame's matrix before its reads complete; one buffer and stable descriptor bindings per View are sufficient. Moving reads or writes to another queue or shader stage requires revisiting this guarantee.

DescriptorSetPool.Release defers frees until frame completion. BufferManager.DestroyBuffer waits for device idle before destroying storage. Deactivation removes retained commands before releasing resources. Shader changes rebuild drawable allocations to respect changed schemas.

## Landfall integration checks (requires the external application)

- Activate background, world, and GUI Views together; confirm camera isolation, ordering, and clipping.
- Select one drawable in two Views with distinct cameras; move each camera independently.
- Capture a frame: expect one 64-byte update per rendered View, independent of drawable count.
- Change layer masks, shaders, textures, and pipeline policies while rendering.
- Repeatedly activate/deactivate Views and launch with multiple frames in flight under Vulkan validation; check resource counts and validation output.
- Track the intermittent tan viewport separately. Its cause has not been established; passing projection tests does not close that regression.

The repository unit tests cover View buffer allocation, distinct descriptor bindings and matrix update targets, descriptor reuse, mixed texture bindings, and release. They do not establish the visual Landfall checks above.

## Shader contract boundary

Shared projection currently applies only when the vertex shader's entire uniform layout is one 64-byte InputSemantics.View input at descriptor set 0, binding 0. UsesStandaloneViewUniform names this restriction explicitly. Uniform blocks containing View plus other inputs remain drawable-owned; supporting them requires an explicit binding/offset contract. Additional descriptor bindings (including textures in set 0) are preserved.

Texture updates invalidate mixed View descriptor sets. Release queues their destruction until the last submitted frame completes; it does not free them immediately. ApplyDrawableCommands then refreshes retained View commands before the next submission. Direct CommandFactory callers must perform the same refresh after resource changes.
