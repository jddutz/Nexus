namespace Nexus.Runtime.Network
{
    public enum ConflictResolutionStrategyEnum
    {
        LocalWins,
        RemoteWins,
        OwnerWins,
        TimestampWins,
        Merge,
        Custom,
    }
}
