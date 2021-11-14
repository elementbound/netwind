using UnityEngine;
using Unity.Netcode;
using com.github.elementbound.NetWind;
using System;

public class PlayerMovementController : RewindableStateBehaviour<PlayerMovementController.State>
{
    [Serializable]
    public struct State
    {
        public Vector3 position;

        public override string ToString()
        {
            return $"PlayerState({position})";
        }
    }

    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;

    private void OnEnable()
    {
        inputProvider ??= GetComponent<InputProvider>();
    }

    public override void Simulate(int tick, float deltaTime)
    {
        var input = inputProvider.Current;
        transform.position += input.movement * moveSpeed * deltaTime;
    }

    protected override State CaptureState()
    {
        var state = new State()
        {
            position = transform.position
        };

        return state;
    }

    protected override void ApplyState(State state)
    {
        transform.position = state.position;
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
