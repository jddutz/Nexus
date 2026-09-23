namespace Nexus.Graphics.Vulkan.Rendering;

/// <summary>
/// Stores render batches in the slots defined by the available render passes.
/// </summary>
public sealed class RenderBatchCollection : IEnumerable<IRenderBatch>
{
    private readonly IRenderBatch?[] _batches = new IRenderBatch?[RenderPasses.Count + 2];

    /// <summary>
    /// Gets the number of render-pass slots in the collection.
    /// </summary>
    public int Count => _batches.Length;

    /// <summary>
    /// Gets the batch assigned to a single render-pass slot.
    /// </summary>
    /// <param name="renderPass">A mask containing exactly one render-pass bit.</param>
    /// <returns>The batch assigned to the render pass.</returns>
    public IRenderBatch this[uint renderPass] => Get(renderPass);

    /// <summary>
    /// Gets the batch assigned to a single render-pass slot.
    /// </summary>
    /// <param name="renderPass">A mask containing exactly one render-pass bit.</param>
    /// <returns>The batch assigned to the render pass.</returns>
    public IRenderBatch Get(uint renderPass)
    {
        var index = GetSlotIndex(renderPass);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(renderPass));

        return _batches[index]
            ?? throw new KeyNotFoundException($"No render batch exists for pass {renderPass}.");
    }

    /// <summary>
    /// Attempts to get the batch assigned to a render-pass slot.
    /// </summary>
    /// <param name="renderPass">A mask containing exactly one render-pass bit, or a phase sentinel.</param>
    /// <param name="batch">The assigned batch, or <see langword="null"/> when none is assigned.</param>
    /// <returns><see langword="true"/> when a batch is assigned; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(uint renderPass, out IRenderBatch? batch)
    {
        var index = GetSlotIndex(renderPass);
        if (index < 0)
        {
            batch = null;
            return false;
        }

        batch = _batches[index];
        return batch is not null;
    }

    /// <summary>
    /// Enumerates the batches assigned to this layer in render-pass order.
    /// </summary>
    /// <returns>The assigned batches in render-pass order.</returns>
    public IEnumerator<IRenderBatch> GetEnumerator()
    {
        foreach (var batch in _batches)
        {
            if (batch is not null)
                yield return batch;
        }
    }

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();

    /// <summary>
    /// Assigns a batch to a single render-pass slot.
    /// </summary>
    /// <param name="renderPass">A mask containing exactly one render-pass bit.</param>
    /// <param name="batch">The batch to assign.</param>
    public void Set(uint renderPass, IRenderBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var index = GetSlotIndex(renderPass);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(renderPass));

        _batches[index] = batch;
    }

    /// <summary>
    /// Removes all batches from the collection.
    /// </summary>
    public void Clear() => Array.Clear(_batches);

    private static int GetSlotIndex(uint renderPass) =>
        renderPass switch
        {
            RenderPasses.Start => 0,
            RenderPasses.End => RenderPasses.Count + 1,
            _ => RenderPasses.GetIndex(renderPass) is var index and >= 0 ? index + 1 : -1,
        };
}
