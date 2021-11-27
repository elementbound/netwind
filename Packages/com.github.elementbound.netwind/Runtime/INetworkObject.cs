namespace com.github.elementbound.NetWind
{
    public interface INetworkObject
    {
        ulong NetId { get; }
        bool IsOwn { get; }
    }
}
