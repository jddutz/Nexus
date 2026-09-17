namespace Nexus.Runtime.Network
{
    public class NetworkSyncErrorEventArgs(
        INetworkSync networkSync,
        Exception exception,
        NetworkUpdate? update = null
    ) : EventArgs
    {
        public INetworkSync NetworkSync { get; } = networkSync;
        public Exception Exception { get; } = exception;
        public NetworkUpdate? Update { get; } = update;
        public bool Ignore { get; set; }
    }
}
