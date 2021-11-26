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

        [Header("Runtime")]
        private double nextTickAt;
        private readonly HashSet<IRewindableState> stateHandlers = new HashSet<IRewindableState>();
        private readonly HashSet<IRewindableInput> inputHandlers = new HashSet<IRewindableInput>();

        public int HistorySize => historySize;
        public int DisplayOffset => displayOffset;

        public void RegisterInput(IRewindableInput input)
        {
            inputHandlers.Add(input);
        }

        public void RegisterState(IRewindableState state)
        {
            stateHandlers.Add(state);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            NetworkManager.NetworkTickSystem.Tick += NetworkUpdate;
        }

        private void Update()
        {
            var displayedTick = NetworkManager.LocalTime.Tick - displayOffset;
            var currentTime = NetworkManager.LocalTime.Time;
            var f = (float)(1.0 - (nextTickAt - currentTime) / NetworkManager.LocalTime.FixedDeltaTime);

            foreach (var state in stateHandlers)
                if (state.IsInterpolated)
                    state.InterpolateState(displayedTick - 1, displayedTick, f);
        }

        private void NetworkUpdate()
        {
            int currentTick = NetworkManager.LocalTime.Tick;
            float deltaTime = NetworkManager.LocalTime.FixedDeltaTime;
            nextTickAt = NetworkManager.LocalTime.Time + deltaTime;

            if (IsHost)
            {
                foreach (var input in inputHandlers)
                    if (input.IsOwn)
                        input.SaveInput(currentTick);

                // There's always a new input, since there's a local player with input
                int earliestInput = inputHandlers
                    .Where(input => input.HasNewInput)
                    .Select(input => input.EarliestReceivedInput)
                    .Min();

                foreach (var input in inputHandlers)
                    input.AcknowledgeInputs();

                Debug.Log($"[Host] Resimulating from earliest input {earliestInput} -> {currentTick}");
                for (int tick = earliestInput; tick <= currentTick; ++tick)
                {
                    foreach (var input in inputHandlers)
                        input.RestoreInput(tick);

                    foreach (var state in stateHandlers)
                        state.RestoreState(tick - 1);

                    foreach (var state in stateHandlers)
                        if (state.ControlledBy == null || tick <= state.ControlledBy.LatestKnownInput)
                            state.Simulate(tick, deltaTime);

                    foreach (var state in stateHandlers)
                        if (state.ControlledBy == null || tick <= state.ControlledBy.LatestKnownInput)
                        {
                            state.SaveState(tick);
                            state.CommitState(tick);
                        }
                }

                foreach (var state in stateHandlers)
                {
                    state.RestoreState(currentTick - displayOffset);
                    state.AcknowledgeStates();
                }
            }
            else if (IsClient)
            {
                // Gather and send inputs
                foreach (var input in inputHandlers)
                {
                    if (input.IsOwn)
                    {
                        input.SaveInput(currentTick);
                        input.CommitInput(currentTick);
                    }
                }

                bool hasNewState = stateHandlers
                    .Any(state => state.HasNewState && state.IsOwn);

                // This should always be true tho
                bool hasNewInput = inputHandlers
                    .Any(input => input.IsOwn && input.HasNewInput);
                 
                if (hasNewState || hasNewInput)
                {
                    int resimulateFrom = currentTick;

                    if (hasNewState)
                        resimulateFrom = stateHandlers
                            .Where(state => state.HasNewState)
                            .Where(state => state.IsOwn)
                            .Select(state => state.LatestReceivedState)
                            .Min();

                    Debug.Log($"[Client] Resimulating {resimulateFrom} -> {currentTick}");
                    for (int tick = resimulateFrom; tick <= currentTick; ++tick)
                    {
                        foreach (var input in inputHandlers)
                            if (input.IsOwn)
                                input.RestoreInput(tick);

                        foreach (var state in stateHandlers)
                            state.RestoreState(tick - 1);

                        foreach (var state in stateHandlers)
                            if (state.IsOwn && tick >= state.LatestReceivedState)
                                state.Simulate(tick, deltaTime);
                            else if (state.IsOwn)
                                Debug.Log($"[Client] Skipping simulation of state for tick {tick}, latest known is {state.LatestReceivedState}");

                        foreach (var state in stateHandlers)
                            if (state.IsOwn && tick >= state.LatestReceivedState)
                                state.SaveState(tick);
                    }
                }

                foreach (var state in stateHandlers)
                {
                    state.AcknowledgeStates();
                    state.RestoreState(currentTick - displayOffset);
                }

                foreach (var input in inputHandlers)
                    input.AcknowledgeInputs();
            }
            else if (IsServer)
            {
                bool hasNewInput = inputHandlers.Any(input => input.HasNewInput);

                if (hasNewInput)
                {
                    int earliestInput = inputHandlers
                        .Where(input => input.HasNewInput)
                        .Select(input => input.EarliestReceivedInput)
                        .Min();

                    foreach (var input in inputHandlers)
                        input.AcknowledgeInputs();

                    for (int tick = earliestInput; tick <= currentTick; ++tick)
                    {
                        foreach (var input in inputHandlers)
                            input.RestoreInput(tick);

                        foreach (var state in stateHandlers)
                            state.RestoreState(tick - 1);

                        foreach (var state in stateHandlers)
                            if (state.ControlledBy != null && state.ControlledBy.LatestKnownInput >= tick)
                                state.Simulate(tick, deltaTime);

                        foreach (var state in stateHandlers)
                            if (state.ControlledBy != null && state.ControlledBy.LatestKnownInput >= tick)
                            {
                                state.SaveState(tick);
                                state.CommitState(tick);
                            }
                    }

                    foreach (var state in stateHandlers)
                        state.RestoreState(currentTick - displayOffset);
                }
            }
        }
    }
}
