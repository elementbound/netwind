using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace com.github.elementbound.NetWind
{
    public class NetworkRewindManager : NetworkBehaviour
    {
        private static NetworkRewindManager instance;

        public static NetworkRewindManager Instance =>
            instance ??= FindObjectOfType<NetworkRewindManager>();

        [Header("Configuration")]
        [SerializeField] private int displayOffset = 2;
        [SerializeField] private int historySize = 64;
        [SerializeField] private bool enableSyncPhysicsOnRestore;
        [SerializeField] private bool enableStepPhysicsOnSimulate;

        [Header("Runtime")]
        private double nextTickAt;
        private readonly DeferredMutableSet<IRewindableObject> rewindableObjects = new DeferredMutableSet<IRewindableObject>();

        public int HistorySize => historySize;
        public int DisplayOffset => displayOffset;
        public double Time { get; private set; }

        public NetworkRewindEvents RewindEvents { get; } = new NetworkRewindEvents();

        public void RegisterRewindable(IRewindableObject rewindable)
        {
            rewindableObjects.Add(rewindable);
        }

        public void RemoveRewindable(IRewindableObject rewindable)
        {
            rewindableObjects.Remove(rewindable);
        }

        private IEnumerable<IRewindableState> GetStates()
        {
            return rewindableObjects.SelectMany(rewindable => rewindable.GetStates());
        }

        private IEnumerable<IRewindableInput> GetInputs()
        {
            return rewindableObjects.SelectMany(rewindable => rewindable.GetInputs());
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            NetworkManager.NetworkTickSystem.Tick += NetworkUpdate;
            Time = 0;

            if (enableSyncPhysicsOnRestore)
                RewindEvents.OnTickRestore += tick => Physics.SyncTransforms();

            if (enableStepPhysicsOnSimulate)
                RewindEvents.OnTickSimulate += tick => Physics.Simulate(NetworkManager.LocalTime.FixedDeltaTime);
        }

        private void Update()
        {
            var displayedTick = NetworkManager.LocalTime.Tick - displayOffset;
            var currentTime = NetworkManager.LocalTime.Time;
            var f = (float)(1.0 - (nextTickAt - currentTime) / NetworkManager.LocalTime.FixedDeltaTime);

            foreach (var state in GetStates())
                if (state.IsInterpolated)
                    state.InterpolateState(displayedTick - 1, displayedTick, f);
        }

        private void NetworkUpdate()
        {
            int currentTick = NetworkManager.LocalTime.Tick;
            float deltaTime = NetworkManager.LocalTime.FixedDeltaTime;
            nextTickAt = NetworkManager.LocalTime.Time + deltaTime;

            rewindableObjects.AcknowledgeMutations();

            if (IsHost)
            {
                foreach (var input in GetInputs())
                    if (input.IsOwn)
                        input.SaveInput(currentTick);

                // There's always a new input, since there's a local player with input
                int earliestInput = GetInputs()
                    .Where(input => input.HasNewInput)
                    .Select(input => input.EarliestReceivedInput)
                    .Min();

                foreach (var input in GetInputs())
                    input.AcknowledgeInputs();

                Debug.Log($"[Host] Resimulating from earliest input {earliestInput} -> {currentTick}");
                RewindEvents.BeforeResimulate?.Invoke(earliestInput, currentTick);

                for (int tick = earliestInput; tick <= currentTick; ++tick)
                {
                    Time = tick * NetworkManager.LocalTime.FixedDeltaTime;

                    foreach (var input in GetInputs())
                        input.RestoreInput(tick);

                    foreach (var state in GetStates())
                        state.RestoreState(tick - 1);

                    ApplyAlivenessForTick(tick);

                    RewindEvents.OnTickRestore?.Invoke(tick);

                    foreach (var state in GetStates())
                        if (state.ControlledBy == null || tick <= state.ControlledBy.LatestKnownInput)
                            state.Simulate(tick, deltaTime);

                    RewindEvents.OnTickSimulate?.Invoke(tick);

                    foreach (var state in GetStates())
                        if (state.ControlledBy == null || tick <= state.ControlledBy.LatestKnownInput)
                        {
                            state.SaveState(tick);
                            state.CommitState(tick);
                        }
                }

                // Destroy rewindables marked for destroy a sufficient time ago
                // ( so that we won't rewind to a time where they exist )
                foreach (var netObj in GetNetworkObjectsToDelete(currentTick))
                    netObj.Despawn();

                ApplyAlivenessForTick(currentTick - displayOffset);

                foreach (var state in GetStates())
                {
                    state.RestoreState(currentTick - displayOffset);
                    state.AcknowledgeStates();
                }
                RewindEvents.OnVisualRestore?.Invoke(currentTick - displayOffset);
            }
            else if (IsClient)
            {
                // Gather and send inputs
                foreach (var input in GetInputs())
                {
                    if (input.IsOwn)
                    {
                        input.SaveInput(currentTick);
                        input.CommitInput(currentTick);
                    }
                }

                bool hasNewState = GetStates()
                    .Any(state => state.HasNewState && state.IsOwn);

                // This should always be true tho
                bool hasNewInput = GetInputs()
                    .Any(input => input.IsOwn && input.HasNewInput);
                 
                if (hasNewState || hasNewInput)
                {
                    int resimulateFrom = currentTick;

                    if (hasNewState)
                        resimulateFrom = GetStates()
                            .Where(state => state.HasNewState)
                            .Where(state => state.IsOwn)
                            .Select(state => state.LatestReceivedState)
                            .Min();

                    Debug.Log($"[Client] Resimulating {resimulateFrom} -> {currentTick}");
                    RewindEvents.BeforeResimulate?.Invoke(resimulateFrom, currentTick);

                    for (int tick = resimulateFrom; tick <= currentTick; ++tick)
                    {
                        foreach (var input in GetInputs())
                            if (input.IsOwn)
                                input.RestoreInput(tick);

                        foreach (var state in GetStates())
                            state.RestoreState(tick - 1);

                        ApplyAlivenessForTick(tick);

                        RewindEvents.OnTickRestore?.Invoke(tick);

                        foreach (var state in GetStates())
                            if (state.IsOwn && tick >= state.LatestReceivedState)
                                state.Simulate(tick, deltaTime);
                            else if (state.IsOwn)
                                Debug.Log($"[Client] Skipping simulation of state for tick {tick}, latest known is {state.LatestReceivedState}");

                        RewindEvents.OnTickSimulate?.Invoke(tick);

                        foreach (var state in GetStates())
                            if (state.IsOwn && tick >= state.LatestReceivedState)
                                state.SaveState(tick);
                    }
                }

                ApplyAlivenessForTick(currentTick - displayOffset);

                foreach (var state in GetStates())
                {
                    state.AcknowledgeStates();
                    state.RestoreState(currentTick - displayOffset);
                }

                RewindEvents.OnVisualRestore?.Invoke(currentTick - displayOffset);

                foreach (var input in GetInputs())
                    input.AcknowledgeInputs();
            }
            else if (IsServer)
            {
                bool hasNewInput = GetInputs().Any(input => input.HasNewInput);

                if (hasNewInput)
                {
                    int earliestInput = GetInputs()
                        .Where(input => input.HasNewInput)
                        .Select(input => input.EarliestReceivedInput)
                        .Min();

                    foreach (var input in GetInputs())
                        input.AcknowledgeInputs();

                    RewindEvents.BeforeResimulate?.Invoke(earliestInput, currentTick);
                    for (int tick = earliestInput; tick <= currentTick; ++tick)
                    {
                        foreach (var input in GetInputs())
                            input.RestoreInput(tick);

                        foreach (var state in GetStates())
                            state.RestoreState(tick - 1);

                        ApplyAlivenessForTick(tick);

                        RewindEvents.OnTickRestore?.Invoke(tick);

                        foreach (var state in GetStates())
                            if (state.ControlledBy != null && state.ControlledBy.LatestKnownInput >= tick)
                                state.Simulate(tick, deltaTime);

                        RewindEvents.OnTickSimulate?.Invoke(tick);

                        foreach (var state in GetStates())
                            if (state.ControlledBy != null && state.ControlledBy.LatestKnownInput >= tick)
                            {
                                state.SaveState(tick);
                                state.CommitState(tick);
                            }
                    }

                    // Destroy rewindables marked for destroy a sufficient time ago
                    // ( so that we won't rewind to a time where they exist )
                    foreach (var netObj in GetNetworkObjectsToDelete(currentTick))
                        netObj.Despawn();

                    ApplyAlivenessForTick(currentTick - displayOffset);

                    foreach (var state in GetStates())
                        state.RestoreState(currentTick - displayOffset);

                    RewindEvents.OnVisualRestore?.Invoke(currentTick - displayOffset);
                }
            }
        }

        private void ApplyAlivenessForTick(int tick)
        {
            foreach (var rewindable in rewindableObjects)
            {
                if (!rewindable.GetDestroyMark().HasValue)
                    continue;

                var netObj = NetworkManager.SpawnManager.SpawnedObjects[rewindable.NetId];
                netObj.gameObject.SetActive(tick < rewindable.GetDestroyMark().Value);
            }
        }

        private IEnumerable<NetworkObject> GetNetworkObjectsToDelete(int currentTick)
        {
            return rewindableObjects
                                .Where(rewindable => rewindable.GetDestroyMark().HasValue)
                                .Where(rewindable => rewindable.GetDestroyMark().Value < currentTick - historySize)
                                .Select(rewindable => rewindable.NetId)
                                .Select(netId => NetworkManager.SpawnManager.SpawnedObjects[netId]);
        }
    }
}
