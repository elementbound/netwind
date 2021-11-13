using UnityEngine;
using Unity.Netcode;
using System;

namespace com.github.elementbound.NetWind
{
    public abstract class RewindableInputBehaviour<T> : NetworkBehaviour, IRewindableInput
    {
        [Header("NetWind")]
        [SerializeField] private TickHistoryBuffer<T> inputBuffer;
        [SerializeField] private int earliestReceivedInput;

        public ulong NetId => NetworkObjectId;
        public bool IsOwn => IsOwner;

        public int EarliestReceivedInput => earliestReceivedInput;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            inputBuffer = new TickHistoryBuffer<T>(NetworkRewindManager.Instance.HistorySize, NetworkManager.LocalTime.Tick);
            NetworkRewindManager.Instance.RegisterInput(this);
        }

        protected abstract T CaptureInput();
        protected abstract void ApplyInput(T input);

        public void RestoreInput(int tick)
        {
            ApplyInput(inputBuffer.Get(tick));
        }

        public void SaveInput(int tick)
        {
            inputBuffer.Set(CaptureInput(), tick);
        }

        public void CommitInput(int tick)
        {
            CommitInput(inputBuffer.Get(tick), tick);
        }

        protected abstract void CommitInput(T state, int tick);

        protected void HandleInputCommit(T state, int tick)
        {
            inputBuffer.Set(state, tick);
            earliestReceivedInput = Math.Min(tick, earliestReceivedInput);
        }
    }
}