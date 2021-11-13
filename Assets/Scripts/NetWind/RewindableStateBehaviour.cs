using System;
using Unity.Netcode;
using UnityEngine;

namespace com.github.elementbound.NetWind
{
    public abstract class RewindableStateBehaviour<T> : NetworkBehaviour, IRewindableState
    {
        [Header("NetWind")]
        [SerializeField] private TickHistoryBuffer<T> stateBuffer;
        [SerializeField] private int latestReceivedState = 0;
        [SerializeField] private bool hasNewState = false;
        [SerializeField] private IRewindableInput controlledBy;

        public ulong NetId => NetworkObjectId;

        public bool IsOwn => IsOwner;

        public int LatestReceivedState => latestReceivedState;

        public bool HasNewState => hasNewState;

        public IRewindableInput ControlledBy => controlledBy;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            int currentTick = NetworkManager.LocalTime.Tick;
            stateBuffer = new TickHistoryBuffer<T>(NetworkRewindManager.Instance.HistorySize, currentTick);

            var state = CaptureState();
            for (int i = 0; i < NetworkRewindManager.Instance.HistorySize; ++i)
                stateBuffer.Set(state, currentTick - i);

            if (controlledBy == null)
                controlledBy = GetComponent<IRewindableInput>();

            NetworkRewindManager.Instance.RegisterState(this);
        }

        protected abstract T CaptureState();
        protected abstract void ApplyState(T state);

        public void RestoreState(int tick)
        {
            Debug.Log($"[State] Restoring state {stateBuffer.Get(tick)} from tick {tick}");
            ApplyState(stateBuffer.Get(tick));
        }

        public void SaveState(int tick)
        {
            T state = CaptureState();
            Debug.Log($"[State] Saving state {state} for tick {tick}");
            stateBuffer.Set(state, tick);
        }

        public void CommitState(int tick)
        {
            Debug.Log($"[State] Restoring state {stateBuffer.Get(tick)} for tick {tick}");
            CommitState(stateBuffer.Get(tick), tick);
        }

        public abstract void Simulate(int tick, float deltaTime);

        protected abstract void CommitState(T state, int tick);

        protected void HandleStateCommit(T state, int tick)
        {
            stateBuffer.Set(state, tick);
            latestReceivedState = Math.Max(tick, latestReceivedState);
            hasNewState = true;
        }

        public void AcknowledgeStates()
        {
            hasNewState = false;
        }
    }
}
