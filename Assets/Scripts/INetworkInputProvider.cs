using Unity.Netcode;

public interface INetworkInputProvider
{
    ulong NetId { get; }
    bool IsOwn { get; }
    void CaptureInput(FastBufferWriter writer);
    void ApplyInput(FastBufferReader reader);
}
