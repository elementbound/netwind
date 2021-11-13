using Unity.Netcode;

public interface INetworkStateProvider
{
    ulong NetId { get; }
    bool IsOwn { get; }
    void CaptureState(FastBufferWriter writer);
    void ApplyState(FastBufferReader reader);
}
