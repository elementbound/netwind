using Unity.Netcode;
using UnityEngine;

public class TransformNetworkStateProvider : NetworkBehaviour, INetworkStateProvider
{
    private static readonly int STATE_SIZE = 10 * 4;

    public ulong NetId => NetworkObjectId;

    public bool IsOwn => IsOwner;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        NetworkHistoryManager.Singleton.RegisterStateProvider(this);
    }

    public void ApplyState(FastBufferReader reader)
    {
        reader.TryBeginRead(STATE_SIZE);

        reader.ReadValue(out Vector3 localPosition);
        reader.ReadValue(out Vector3 localScale);
        reader.ReadValue(out Quaternion localRotation);

        transform.localPosition = localPosition;
        transform.localScale = localScale;
        transform.localRotation = localRotation;
    }

    public void CaptureState(FastBufferWriter writer)
    {
        writer.TryBeginWrite(STATE_SIZE);

        writer.WriteValue(transform.localPosition);
        writer.WriteValue(transform.localScale);
        writer.WriteValue(transform.localRotation);
    }
}
