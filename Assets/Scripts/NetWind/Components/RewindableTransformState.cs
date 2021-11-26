using UnityEngine;
using Unity.Netcode;
using System;

namespace com.github.elementbound.NetWind
{
    public class RewindableTransformState : RewindableStateBehaviour<RewindableTransformState.State>
    {
        [Serializable]
        public struct State
        {
            public Vector3 localPosition;
            public Vector3 localScale;
            public Quaternion localRotation;
        }

        protected override State CaptureState()
        {
            return new State()
            {
                localPosition = transform.localPosition,
                localScale = transform.localScale,
                localRotation = transform.localRotation
            };
        }

        protected override void ApplyState(State state)
        {
            transform.localPosition = state.localPosition;
            transform.localScale = state.localScale;
            transform.localRotation = state.localRotation;
        }

        public override void Simulate(int tick, float deltaTime)
        {
        }

        protected override void CommitState(State state, int tick)
        {
            CommitStateClientRpc(state, tick);
        }

        [ClientRpc]
        private void CommitStateClientRpc(State state, int tick)
        {
            HandleStateCommit(state, tick);
        }
    }
}
