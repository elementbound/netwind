using System;
using UnityEngine;
using Unity.Netcode;

public class InputProvider : NetworkBehaviour, INetworkInputProvider
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
            NetworkHistoryManager.Singleton.RegisterInputProvider(this);
    }

    private void OnEnable()
    {
        NetworkManager.NetworkTickSystem.Tick += NetworkUpdate;
    }

    private void OnDisable()
    {
        if (NetworkManager)
            NetworkManager.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    private void Update()
    {
        if (IsOwner)
        {
            var movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
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
        }
    }

    public void CaptureInput(FastBufferWriter writer)
    {
        writer.WriteValueSafe(currentInput.movement);
    }

    public void ApplyInput(FastBufferReader reader)
    {
        reader.ReadValueSafe(out currentInput.movement);
    }

    public State Current => currentInput;

    public ulong NetId => NetworkObjectId;

    public bool IsOwn => IsOwner;
}
