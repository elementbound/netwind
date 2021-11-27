using System;
using UnityEngine;
using com.github.elementbound.NetWind;
using Unity.Netcode;

public class InputProvider : RewindableInputBehaviour<InputProvider.State>
{
    [Serializable]
    public struct State
    {
        public Vector3 movement;

        public void Reset()
        {
            movement = Vector3.zero;
        }

        public State AverageOver(int sampleCount)
        {
            sampleCount = sampleCount > 0 ? sampleCount : 1;

            return new State()
            {
                movement = this.movement / sampleCount
            };
        }

        public override string ToString()
        {
            return $"Input({movement})";
        }
    }

    [Header("Runtime")]
    [SerializeField] private State currentInput;
    [SerializeField] private State cumulativeInput;
    [SerializeField] private int sampleCount;

    private void Update()
    {
        if (IsOwn)
        {
            var movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (movement.magnitude > 1f)
                movement.Normalize();

            cumulativeInput.movement += movement;
            sampleCount++;
        }
    }

    protected override State CaptureInput()
    {
        currentInput = cumulativeInput.AverageOver(sampleCount);
        cumulativeInput.Reset();
        sampleCount = 0;

        return currentInput;
    }

    protected override void ApplyInput(State input)
    {
        currentInput = input;
        cumulativeInput.Reset();
        sampleCount = 0;
    }

    protected override void CommitInput(State state, int tick)
    {
        CommitInputServerRpc(state, tick);
    }

    [ServerRpc]
    private void CommitInputServerRpc(State state, int tick)
    {
        HandleInputCommit(state, tick);
    }

    public State Current => currentInput;
}
