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

        private void NetworkUpdate()
        {
            int currentTick = NetworkManager.LocalTime.Tick;
            float deltaTime = NetworkManager.LocalTime.FixedDeltaTime;


            if (IsHost)
            {
                foreach (var input in inputHandlers)
                    if (input.IsOwn)
                        input.SaveInput(currentTick);

                int earliestInput = inputHandlers
                    .Where(input => input.HasNewInput)
                    .Select(input => input.EarliestReceivedInput)
                    .Min();

                Debug.Log($"[Host] Received new input, earliest is {earliestInput} ( {currentTick - earliestInput} ago )");

                foreach (var input in inputHandlers)
                    input.AcknowledgeInputs();

                Debug.Log($"[Host] Resimulating from {earliestInput} to {currentTick}");
                for (int tick = earliestInput; tick <= currentTick; ++tick)
                {
                    foreach (var input in inputHandlers)
                        input.RestoreInput(tick);

                    foreach (var state in stateHandlers)
                        state.RestoreState(tick - 1);

                    foreach (var state in stateHandlers)
                    {
                        if (state.ControlledBy == null || state.ControlledBy.LatestKnownInput < tick)
                            continue;

                        state.Simulate(tick, deltaTime);
                        state.SaveState(tick);
                        state.CommitState(tick);
                    }
                }

                foreach (var state in stateHandlers)
                    state.RestoreState(currentTick - displayOffset);
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

                bool hasNewState = stateHandlers.Any(state => state.HasNewState && state.IsOwn);

                if (hasNewState && !IsHost)
                {
                    int resimulateFrom = stateHandlers
                        .Where(state => state.HasNewState)
                        .Where(state => state.IsOwn)
                        .Select(state => state.LatestReceivedState)
                        .Min();

                    Debug.Log($"[Client] Resimulating from earliest authorative tick {resimulateFrom} to {currentTick}");
                    for (int tick = resimulateFrom; tick <= currentTick; ++tick)
                    {
                        foreach (var input in inputHandlers)
                            input.RestoreInput(tick);

                        foreach (var state in stateHandlers)
                            state.RestoreState(tick - 1);

                        foreach (var state in stateHandlers)
                        {
                            if (!state.IsOwn || tick < state.LatestReceivedState)
                                continue;

                            state.Simulate(tick, deltaTime);
                            state.SaveState(tick);
                        }
                    }
                }

                foreach (var state in stateHandlers)
                {
                    state.AcknowledgeStates();
                    state.RestoreState(currentTick - displayOffset);
                }
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

                    Debug.Log($"[Server] Received new input, earliest is {earliestInput} ( {currentTick - earliestInput} ago )");

                    foreach (var input in inputHandlers)
                        input.AcknowledgeInputs();

                    Debug.Log($"[Server] Resimulating from {earliestInput} to {currentTick}");
                    for (int tick = earliestInput; tick <= currentTick; ++tick)
                    {
                        foreach (var input in inputHandlers)
                            input.RestoreInput(tick);

                        foreach (var state in stateHandlers)
                            state.RestoreState(tick - 1);

                        foreach (var state in stateHandlers)
                        {
                            if (state.ControlledBy == null || state.ControlledBy.LatestKnownInput < tick)
                                continue;

                            state.Simulate(tick, deltaTime);
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
