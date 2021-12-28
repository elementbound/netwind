using Unity.Netcode;
using UnityEngine;

namespace com.github.elementbound.NetWind
{
    public class RewindableNetworkObject : NetworkBehaviour, IRewindableObject
    {
        public ulong NetId => NetworkObjectId;

        public bool IsOwn => IsOwner || (IsOwnedByServer && IsServer);

        [Header("Runtime")]
        [SerializeField] private int? destroyTick;
        private IRewindableInput[] inputs = null;
        private IRewindableState[] states = null;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // LIMITATION: Doesn't support adding/removing components on the go
            // If we could somehow subscribe to add/remove events, that would be dope
            inputs = GetComponents<IRewindableInput>();
            states = GetComponents<IRewindableState>();
            NetworkRewindManager.Instance.RegisterRewindable(this);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            NetworkRewindManager.Instance.RemoveRewindable(this);
        }

        public IRewindableInput[] GetInputs()
        {
            return inputs;
        }

        public IRewindableState[] GetStates()
        {
            return states;
        }

        public void MarkForDestroy()
        {
            MarkForDestroy(NetworkManager.LocalTime.Tick);
        }

        public void MarkForDestroy(int tick)
        {
            if (!IsServer)
            {
                // LIMITATION: Clients can't mark objects for destroy
                // TODO: Support client as well?
                Debug.LogError("Only the server can mark object for destroy!");
                return;
            }

            if (!destroyTick.HasValue || tick < destroyTick.Value)
            {
                destroyTick = tick;
                NotifyDestroyMarkClientRpc(tick);
            }
        }

        public int? GetDestroyMark()
        {
            return destroyTick;
        }

        [ClientRpc]
        private void NotifyDestroyMarkClientRpc(int tick) {
            destroyTick = tick;
        }
    }
}