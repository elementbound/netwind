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
            sampleCount = sampleCount > 0 ? sampleCount : 1;

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

    private void OnEnable()
    {
        NetworkManager.NetworkTickSystem.Tick += NetworkUpdate;
    }

    private void OnDisable()
    {
        NetworkManager.NetworkTickSystem.Tick -= NetworkUpdate;
    }

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

    private void NetworkUpdate()
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
