using System;
using UnityEngine;
using Unity.Netcode;

public class InputProvider : NetworkBehaviour
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
            return new State()
            {
                movement = this.movement / sampleCount
            };
        }
    }

    [Header("Runtime")]
    [SerializeField] private State currentInput;
    [SerializeField] private State cumulativeInput;
    [SerializeField] private int sampleCount;

    private void Update()
    {
        if (IsOwner)
        {
            var movement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            if (movement.magnitude > 1f)
                movement.Normalize();

            cumulativeInput.movement += movement;
            sampleCount++;
        }
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            currentInput = cumulativeInput.AverageOver(sampleCount);
            cumulativeInput.Reset();
            sampleCount = 0;

            SubmitInputServerRpc(currentInput);
        }
    }

    [ServerRpc]
    private void SubmitInputServerRpc(State state)
    {
        currentInput = state;
    }

    public State Current => currentInput;
}
