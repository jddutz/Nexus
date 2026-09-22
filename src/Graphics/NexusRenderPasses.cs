namespace Nexus.Graphics;

/// <summary>
/// Defines standard render-pass masks and utilities for selecting render passes.
/// </summary>
/// <remarks>
/// Render passes execute in bit order, from the lowest set bit to the highest set bit.
/// Multiple passes can be combined with bitwise OR.
/// </remarks>
public static class NexusRenderPasses
{
    /// <summary>Shadow map generation pass.</summary>
    public const uint Shadow = 1u << 0;

    /// <summary>Depth prepass for early depth testing.</summary>
    public const uint Depth = 1u << 1;

    /// <summary>Main opaque geometry pass.</summary>
    public const uint Main = 1u << 2;

    /// <summary>Deferred lighting accumulation pass.</summary>
    public const uint Lighting = 1u << 3;

    /// <summary>Reflection rendering pass.</summary>
    public const uint Reflection = 1u << 4;

    /// <summary>Transparent geometry and particle pass.</summary>
    public const uint Transparent = 1u << 5;

    /// <summary>Post-processing pass.</summary>
    public const uint Post = 1u << 6;

    /// <summary>Screen-space UI and debug overlay pass.</summary>
    public const uint UI = 1u << 7;

    /// <summary>All render passes combined.</summary>
    public const uint All = uint.MaxValue;

    /// <summary>All opaque passes: shadow, depth, and main.</summary>
    public const uint Opaque = Shadow | Depth | Main;

    /// <summary>The alpha-blended transparent pass.</summary>
    public const uint AlphaBlended = Transparent;

    /// <summary>All scene passes, excluding post-processing and UI.</summary>
    public const uint Scene = Shadow | Depth | Main | Lighting | Reflection | Transparent;

    /// <summary>The total number of defined render passes.</summary>
    public const int Count = 8;

    /// <summary>The maximum valid render-pass bit index.</summary>
    public const int MaxBitIndex = 7;

    /// <summary>Gets the human-readable name of a single render pass.</summary>
    /// <param name="pass">A mask containing exactly one render-pass bit.</param>
    /// <returns>The pass name, or an unknown-pass label when the mask is invalid.</returns>
    public static string GetName(uint pass) =>
        pass switch
        {
            Shadow => nameof(Shadow),
            Depth => nameof(Depth),
            Main => nameof(Main),
            Lighting => nameof(Lighting),
            Reflection => nameof(Reflection),
            Transparent => nameof(Transparent),
            Post => nameof(Post),
            UI => nameof(UI),
            _ => $"Unknown(0x{pass:X})",
        };

    /// <summary>Gets the names of all passes enabled in a combined mask.</summary>
    /// <param name="mask">A mask containing one or more render passes.</param>
    /// <returns>A comma-separated list of pass names.</returns>
    public static string GetNames(uint mask)
    {
        if (mask == 0)
            return "None";
        if (mask == All)
            return "All";

        var names = new List<string>();
        for (int i = 0; i <= MaxBitIndex; i++)
        {
            uint bit = 1u << i;
            if ((mask & bit) != 0)
                names.Add(GetName(bit));
        }

        return string.Join(", ", names);
    }

    /// <summary>Gets the bit index of a single render pass.</summary>
    /// <param name="pass">A mask containing exactly one render-pass bit.</param>
    /// <returns>The bit index, or -1 when the mask is invalid.</returns>
    public static int GetIndex(uint pass)
    {
        if (pass == 0 || (pass & (pass - 1)) != 0)
            return -1;

        int index = 0;
        while ((pass & 1) == 0)
        {
            pass >>= 1;
            index++;
        }
        return index;
    }

    /// <summary>Enumerates all active passes in execution order.</summary>
    /// <param name="mask">A mask containing one or more render passes.</param>
    /// <returns>The individual pass bits from lowest to highest bit.</returns>
    public static IEnumerable<uint> GetActivePasses(uint mask)
    {
        for (int i = 0; i <= MaxBitIndex; i++)
        {
            uint pass = 1u << i;
            if ((mask & pass) != 0)
                yield return pass;
        }
    }

    /// <summary>Determines whether a specific pass is enabled in a mask.</summary>
    /// <param name="mask">The mask to inspect.</param>
    /// <param name="pass">The pass bit to find.</param>
    /// <returns><see langword="true"/> when the pass is enabled; otherwise, <see langword="false"/>.</returns>
    public static bool HasPass(uint mask, uint pass) => (mask & pass) != 0;

    /// <summary>Counts the number of active passes in a mask.</summary>
    /// <param name="mask">The mask to inspect.</param>
    /// <returns>The number of set render-pass bits.</returns>
    public static int CountPasses(uint mask)
    {
        int count = 0;
        while (mask != 0)
        {
            count += (int)(mask & 1);
            mask >>= 1;
        }
        return count;
    }
}
