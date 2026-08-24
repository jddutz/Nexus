namespace Nexus.Graphics;

/// <summary>
/// Event args for batching statistics, raised by <see cref="Abstractions.IRenderer"/>.
/// </summary>
public class BatchingStatisticsEventArgs : EventArgs
{
    public uint PassIndex { get; init; }
    public string PassName { get; init; } = string.Empty;
    public DefaultBatchStrategy.BatchingStatistics Statistics { get; init; }
}
