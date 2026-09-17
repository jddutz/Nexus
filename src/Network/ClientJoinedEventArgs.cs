namespace Nexus.Runtime.Network
{
    public class ClientJoinedEventArgs(string clientId, Dictionary<string, object> clientInfo)
        : EventArgs
    {
        public string ClientId { get; } = clientId;
        public Dictionary<string, object> ClientInfo { get; } = clientInfo;
    }
}
