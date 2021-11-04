using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerMovementController : NetworkBehaviour
{
    private static readonly int HISTORY_LENGTH = 128;
    private static readonly int DISPLAY_OFFSET = 2;

    [Serializable]
    private struct CharacterState
    {
        public Vector3 position;

        public bool Equals(CharacterState other)
        {
            return Vector3.Distance(this.position, other.position) < 0.05f;
        }
    }

    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool enableInterpolation = true;

    [Header("Dependencies")]
    [SerializeField] private InputProvider inputProvider;

    [Header("Runtime")]
    [SerializeField] private TickHistoryBuffer<InputProvider.State> inputCache;
    [SerializeField] private TickHistoryBuffer<CharacterState> stateCache;

    [SerializeField] private int earliestInput;
    [SerializeField] private int latestState;
    [SerializeField] private bool needsResimulation = false;

    [SerializeField] private CharacterState fromState;
    [SerializeField] private CharacterState toState;
    [SerializeField] private double fromTime;
    [SerializeField] private double toTime;

    private void OnEnable()
    {
        inputProvider ??= GetComponent<InputProvider>();

        NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkUpdate;

        fromState = CaptureState();
        toState = CaptureState();
    }

    private void OnDisable()
    {
        if (NetworkManager)
            NetworkManager.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        int currentTick = NetworkManager.LocalTime.Tick;
        earliestInput = currentTick;
        latestState = currentTick;

        inputCache = new TickHistoryBuffer<InputProvider.State>(HISTORY_LENGTH, currentTick);
        stateCache = new TickHistoryBuffer<CharacterState>(HISTORY_LENGTH, currentTick);

        // Backfill state cache
        var state = CaptureState();

        for (int i = 0; i <= DISPLAY_OFFSET + 1; ++i)
            stateCache.Set(state, currentTick - i);
    }

    private void Update()
    {
        if (enableInterpolation)
        {
            var currentTime = NetworkManager.LocalTime.Time;
            var f = 1.0 - (toTime - currentTime) / (toTime - fromTime);

            var interpolatedState = new CharacterState()
            {
                position = Vector3.Lerp(fromState.position, toState.position, (float)f)
            };

            ApplyState(interpolatedState);
        } else
        {
            ApplyState(toState);
        }
    }

    private void NetworkUpdate()
    {
        float deltaTime = NetworkManager.LocalTime.FixedDeltaTime;

        int currentTick = NetworkManager.LocalTime.Tick;

        fromState = CaptureState();
        fromTime = NetworkManager.LocalTime.Time;
        toTime = fromTime + deltaTime;

        if (!IsLocalPlayer && IsServer)
        {
            if (needsResimulation)
            {
                ResimulateFrom(earliestInput, inputCache.GetLatestKnownFrame(), deltaTime);

                needsResimulation = false;
                earliestInput = currentTick;
            }
        }

        if (IsLocalPlayer && !IsServer)
        {
            inputCache.Set(inputProvider.Current, currentTick);
            CommitInputServerRpc(inputCache.Get(currentTick), currentTick);

            if (needsResimulation)
            {
                ResimulateFrom(latestState, currentTick, deltaTime);

                needsResimulation = false;
                latestState = 0;
            }
        }

        if (IsLocalPlayer && IsServer)
        {
            inputCache.Set(inputProvider.Current, currentTick);

            stateCache.Set(Simulate(inputCache.Get(currentTick), stateCache.Get(currentTick - 1), deltaTime), currentTick);
            CommitStateClientRpc(stateCache.Get(currentTick), currentTick);
        }

        toState = stateCache.Get(Math.Min(currentTick - DISPLAY_OFFSET, stateCache.GetLatestKnownFrame()));
    }

    private void ResimulateFrom(int from, int to, float deltaTime)
    {
        for (int tick = from; tick <= to; ++tick)
        {
            stateCache.Set(Simulate(inputCache.Get(tick), stateCache.Get(tick - 1), deltaTime), tick);

            if (IsServer)
                CommitStateClientRpc(stateCache.Get(tick), tick);
        }
    }

    private CharacterState Simulate(InputProvider.State input, CharacterState sourceState, float deltaTime)
    {
        ApplyState(sourceState);

        transform.position += input.movement * moveSpeed * deltaTime;

        return CaptureState();
    }

    private void ApplyState(CharacterState state)
    {
        transform.position = state.position;
    }

    private CharacterState CaptureState()
    {
        return new CharacterState()
        {
            position = transform.position
        };
    }

    [ClientRpc]
    private void CommitStateClientRpc(CharacterState state, int tick)
    {
        if (IsServer)
            return;

        if (stateCache.Get(tick).Equals(state))
            // Update state to its existing value, so it doesn't go stale
            stateCache.Set(stateCache.Get(tick), tick);

        stateCache.Set(state, tick);

        latestState = Math.Max(latestState, tick);
        needsResimulation = true;
    }

    [ServerRpc]
    private void CommitInputServerRpc(InputProvider.State input, int tick)
    {
        inputCache.Set(input, tick);

        earliestInput = Math.Min(earliestInput, tick);
        needsResimulation = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        var currentTime = NetworkManager.LocalTime.Time;
        var f = 1.0 - (toTime - currentTime) / (toTime - fromTime);

        var interpolatedState = new CharacterState()
        {
            position = Vector3.Lerp(fromState.position, toState.position, (float)f)
        };

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(fromState.position, 1f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(toState.position, 1f);

        Gizmos.color = Color.Lerp(Color.red, Color.green, (float)f);
        Gizmos.DrawWireSphere(interpolatedState.position, 0.5f);
    }
}
