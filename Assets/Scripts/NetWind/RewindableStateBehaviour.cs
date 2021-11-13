using Unity.Netcode;
using UnityEngine;

namespace com.github.elementbound.NetWind
{
    public abstract class RewindableStateBehaviour<T> : NetworkBehaviour, IRewindableState
    {
        [Header("NetWind")]
        [SerializeField] private TickHistoryBuffer<T> stateBuffer;

        public ulong NetId => NetworkObjectId;

        public bool IsOwn => IsOwner;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            stateBuffer = new TickHistoryBuffer<T>(NetworkRewindManager.Instance.HistorySize, NetworkManager.LocalTime.Tick);
        }

        protected abstract T CaptureState();
        protected abstract void ApplyState(T state);

        public void RestoreState(int tick)
        {
            ApplyState(stateBuffer.Get(tick));
        }

        public void SaveState(int tick)
        {
            stateBuffer.Set(CaptureState(), tick);
        }

        public abstract void CommitState(int tick);
    }
}
