using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;
using System.Linq;

public class NetworkHistoryManager : NetworkBehaviour
{
    [Serializable]
    struct ObjectDataEntry
    {
        public ulong netId;
        public byte[] data;
    };

    private static NetworkHistoryManager instance;
    public static NetworkHistoryManager Singleton =>
        instance ??= FindObjectOfType<NetworkHistoryManager>();

    public delegate void SimulateEvent(int tickId, float deltaTime);

    public SimulateEvent OnSimulate;

    [Header("Configuration")]
    [SerializeField] private int historyBufferSize = 64;
    [SerializeField] private int maxInputSize = 1024;
    [SerializeField] private int maxStateSize = 1024;

    [Header("Runtime")]
    [SerializeField] private int earliestInput;
    [SerializeField] private bool needsResimulation;
    private Dictionary<ulong, int> latestKnownState = new Dictionary<ulong, int>();

    [Header("Runtime/History")]
    private Dictionary<ulong, INetworkInputProvider> inputProviders = new Dictionary<ulong, INetworkInputProvider>();
    private Dictionary<ulong, INetworkStateProvider> stateProviders = new Dictionary<ulong, INetworkStateProvider>();
    private Dictionary<ulong, TickHistoryBuffer<byte[]>> stateCaches = new Dictionary<ulong, TickHistoryBuffer<byte[]>>();
    private Dictionary<ulong, TickHistoryBuffer<byte[]>> inputCaches = new Dictionary<ulong, TickHistoryBuffer<byte[]>>();

    [Header("Debug")]
    [SerializeField] private int localTick;
    [SerializeField] private GameObject[] inputProviderObjects;
    [SerializeField] private GameObject[] stateProviderObjects;

    private void Start()
    {
        instance = this;
    }

    private void OnEnable()
    {
        if (NetworkManager && NetworkManager.NetworkTickSystem != null)
            NetworkManager.NetworkTickSystem.Tick += NetworkUpdate;
    }

    private void OnDisable()
    {
        if (NetworkManager && NetworkManager.NetworkTickSystem != null)
            NetworkManager.NetworkTickSystem.Tick -= NetworkUpdate;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        NetworkManager.NetworkTickSystem.Tick += NetworkUpdate;
    }

    private void NetworkUpdate()
    {
        localTick = NetworkManager.LocalTime.Tick;

        inputProviderObjects = inputProviders.Values
            .Select(inputProvider => inputProvider.NetId)
            .Select(netId => NetworkManager.SpawnManager.SpawnedObjects[netId])
            .Select(netObject => netObject.gameObject)
            .ToArray();

        stateProviderObjects = stateProviders.Values
            .Select(stateProvider => stateProvider.NetId)
            .Select(netId => NetworkManager.SpawnManager.SpawnedObjects[netId])
            .Select(netObject => netObject.gameObject)
            .ToArray();

        int currentTick = NetworkManager.LocalTime.Tick;
        var deltaTime = NetworkManager.LocalTime.FixedDeltaTime;

        if (inputProviders.Count > 0)
        {
            CaptureInput(currentTick);

            if (IsClient)
                CommitInputServerRpc(FormatInput(currentTick), currentTick);
        }

        if (needsResimulation)
        {
            needsResimulation = false;

            int startingTick = IsServer
                ? earliestInput
                : latestKnownState.Values.Min();

            earliestInput = currentTick;
            latestKnownState.Clear();

            Debug.Log($"Resimulating frames {startingTick} -> {currentTick} on {stateProviders.Count} objects");
            for (int tick = startingTick; tick < currentTick; ++tick)
            {
                var authorativeStates = new List<ObjectDataEntry>();

                // Apply state and input
                ApplyState(tick - 1);
                ApplyInput(tick);

                // Simulate
                OnSimulate?.Invoke(tick, deltaTime);

                // Save state for objects where we know it makes sense
                // ( i.e. their input was known at the frame )
                foreach (var stateProvider in stateProviders.Values)
                {
                    var netId = stateProvider.NetId;

                    if (!inputProviders.ContainsKey(netId))
                    {
                        Debug.LogWarning($"Missing input provider for object {NetworkManager.SpawnManager.SpawnedObjects[netId].gameObject} ( {netId} )");
                        continue;
                    }

                    EnsureInputCacheFor(netId);
                    EnsureStateCacheFor(netId);

                    var inputCache = inputCaches[netId];
                    var stateCache = stateCaches[netId];

                    if (inputCache.GetLatestKnownFrame() < tick)
                        continue;

                    var stateWriter = new FastBufferWriter(4, Unity.Collections.Allocator.Temp, maxStateSize);
                    stateProvider.CaptureState(stateWriter);
                    stateCache.Set(stateWriter.ToArray(), tick);

                    if (IsServer)
                        authorativeStates.Add(new ObjectDataEntry()
                        {
                            netId = netId,
                            data = stateWriter.ToArray()
                        });
                }

                if (IsServer)
                    CommitStateClientRpc(authorativeStates.ToArray(), tick);
            }

            // Apply latest known state to all objects
            Debug.Log($"Applying latest known state for {stateCaches.Count} objects");
            foreach (var netId in stateCaches.Keys)
            {
                var stateProvider = stateProviders[netId];
                var stateCache = stateCaches[netId];
                var latestKnownState = stateCache.Get(stateCache.GetLatestKnownFrame());

                var stateReader = new FastBufferReader(latestKnownState, Unity.Collections.Allocator.Temp);
                stateProvider.ApplyState(stateReader);

                Debug.Log($"Applied latest known frame {stateCache.GetLatestKnownFrame()} on object {NetworkManager.SpawnManager.SpawnedObjects[netId].name}");
            }
        }
    }

    public void RegisterInputProvider(INetworkInputProvider inputProvider)
    {
        if (inputProvider.IsOwn)
            inputProviders[inputProvider.NetId] = inputProvider;
    }

    public void RegisterStateProvider(INetworkStateProvider stateProvider)
    {
        stateProviders[stateProvider.NetId] = stateProvider;
    }

    private void EnsureStateCacheFor(ulong netId)
    {
        if (!stateCaches.ContainsKey(netId))
            stateCaches[netId] = new TickHistoryBuffer<byte[]>(historyBufferSize, NetworkManager.LocalTime.Tick);
    }

    private void EnsureInputCacheFor(ulong netId)
    {
        if (!inputCaches.ContainsKey(netId))
            inputCaches[netId] = new TickHistoryBuffer<byte[]>(historyBufferSize, NetworkManager.LocalTime.Tick);
    }

    private void CaptureState(int tick)
    {
        foreach (var stateProvider in stateProviders.Values)
        {
            EnsureStateCacheFor(stateProvider.NetId);

            var writer = new FastBufferWriter(4, Unity.Collections.Allocator.Temp, maxStateSize);
            stateProvider.CaptureState(writer);
            var state = writer.ToArray();

            stateCaches[stateProvider.NetId].Set(state, tick);
        }
    }

    private void CaptureInput(int tick)
    {
        foreach (var inputProvider in inputProviders.Values)
        {
            EnsureInputCacheFor(inputProvider.NetId);

            var writer = new FastBufferWriter(4, Unity.Collections.Allocator.Temp, maxInputSize);
            inputProvider.CaptureInput(writer);
            var input = writer.ToArray();

            inputCaches[inputProvider.NetId].Set(input, tick);
        }
    }

    private void ApplyState(int tick)
    {
        foreach (var stateEntry in stateCaches)
        {
            var netId = stateEntry.Key;
            var cache = stateEntry.Value;

            try
            {
                var stateProvider = stateProviders[netId];
                var stateReader = new FastBufferReader(cache.Get(tick), Unity.Collections.Allocator.Temp);
                stateProvider.ApplyState(stateReader);
            }
            catch (IndexOutOfRangeException)
            {
                // It's OK
            }
        }
    }

    private void ApplyInput(int tick)
    {
        foreach (var inputEntry in inputCaches)
        {
            var netId = inputEntry.Key;
            var cache = inputEntry.Value;

            try
            {
                var inputProvider = inputProviders[netId];
                var inputReader = new FastBufferReader(cache.Get(tick), Unity.Collections.Allocator.Temp);
                inputProvider.ApplyInput(inputReader);
            }
            catch (IndexOutOfRangeException)
            {
                // It's OK
            }
        }
    }

    private ObjectDataEntry[] FormatInput(int tick)
    {
        var result = new List<ObjectDataEntry>();

        foreach (var inputProvider in inputProviders.Values)
        {
            if (!inputCaches.ContainsKey(inputProvider.NetId))
                continue;

            var inputCache = inputCaches[inputProvider.NetId];
            try
            {
                result.Add(new ObjectDataEntry()
                {
                    netId = inputProvider.NetId,
                    data = inputCache.Get(tick)
                });
            }
            catch (IndexOutOfRangeException)
            {
                // It's OK
            }
        }

        return result.ToArray();
    }

    [ServerRpc(RequireOwnership = false)]
    private void CommitInputServerRpc(ObjectDataEntry[] inputs, int tick)
    {
        foreach (var entry in inputs)
        {
            EnsureInputCacheFor(entry.netId);

            inputCaches[entry.netId].Set(entry.data, tick);
        }

        Debug.Log($"Received input for tick {tick} from client");

        earliestInput = Math.Min(earliestInput, tick);
        needsResimulation = true;
    }

    [ClientRpc]
    private void CommitStateClientRpc(ObjectDataEntry[] states, int tick)
    {
        foreach (var entry in states)
        {
            EnsureStateCacheFor(entry.netId);

            stateCaches[entry.netId].Set(entry.data, tick);
            latestKnownState[entry.netId] = tick;
        }

        Debug.Log($"Received state from server for tick {tick}");
        needsResimulation = true;
    }
}
