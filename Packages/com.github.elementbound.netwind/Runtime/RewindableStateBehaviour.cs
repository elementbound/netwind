using System;
using Unity.Netcode;
using UnityEngine;

namespace com.github.elementbound.NetWind
{
    public abstract class RewindableStateBehaviour<T> : NetworkBehaviour, IRewindableState
    {
        [Header("NetWind")]
        [SerializeField] protected TickHistoryBuffer<T> stateBuffer;
        [SerializeField] private int latestReceivedState = 0;
        [SerializeField] private bool hasNewState = false;

        public ulong NetId => NetworkObjectId;

        public bool IsOwn => IsOwner || (IsOwnedByServer && IsServer);

        public int LatestReceivedState => latestReceivedState;

        public bool HasNewState => hasNewState;

        public IRewindableInput ControlledBy { get; private set; }
        public abstract bool IsInterpolated { get; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            int currentTick = NetworkManager.LocalTime.Tick;
            stateBuffer = new TickHistoryBuffer<T>(NetworkRewindManager.Instance.HistorySize, currentTick);

            var state = CaptureState();
            for (int i = 0; i < NetworkRewindManager.Instance.HistorySize; ++i)
                stateBuffer.Set(state, currentTick - i);

           ControlledBy ??= GetComponent<IRewindableInput>();
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

        public void CommitState(int tick)
        {
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
            latestReceivedState = 0;
        }

        public abstract void InterpolateState(int tickFrom, int tickTo, float f);
    }
}
