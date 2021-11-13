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

            if (IsClient)
            {
                foreach (var inputHandler in inputHandlers)
                    if (inputHandler.IsOwn)
                    {
                        inputHandler.SaveInput(currentTick);
                        inputHandler.CommitInput(currentTick);
                    }

                foreach (var stateHandler in stateHandlers)
                    if (stateHandler.IsOwn)
                        stateHandler.SaveState(currentTick);
            }

            if (IsServer)
            {
                int earliestReceivedInput = inputHandlers
                    .Select(inputHandler => inputHandler.EarliestReceivedInput)
                    .Min();

                // TODO: Simulate from earliest received input
            }
        }
    }
}
